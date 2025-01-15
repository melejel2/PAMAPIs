using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using PAMAPIs.Models;
using PAMAPIs.Services;
using PAMAPIs.Data;
using Syncfusion.Pdf;
using Syncfusion.XlsIO;
using Syncfusion.XlsIORenderer;
using QRCoder;
using SkiaSharp;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace PAM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RequestsController : ControllerBase
    {
        private readonly FuzzyMatchingService _fuzzyMatchingService;
        private readonly PAMContext _dbContext;
        private readonly Common _common;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<LoginController> _logger;
        private readonly IConfiguration _configuration;

        public RequestsController(
            PAMContext dbContext,
            Common common,
            IWebHostEnvironment hostingEnvironment,
            ILoggerFactory loggerFactory,
            FuzzyMatchingService fuzzyMatchingService,
            IConfiguration configuration)
        {
            _fuzzyMatchingService = fuzzyMatchingService;
            _dbContext = dbContext;
            _common = common;
            _hostingEnvironment = hostingEnvironment;
            _logger = loggerFactory.CreateLogger<LoginController>();
            _configuration = configuration;
        }

        [Authorize]
        [HttpGet("newrequest/{siteId}")]
        public async Task<ActionResult<NewRequestData>> GetNewRequestData(int siteId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                if (!CanUserSendRequests(user.RoleId))
                {
                    return Forbid("User does not have permission to send requests.");
                }

                if (!await UserHasAccessToSite(userId, siteId))
                {
                    return Forbid("User does not have access to the specified site.");
                }


                await DeleteTemMaterialRequestItemsAsync(userId, siteId);

                string siteCode = _common.GetSiteCode(siteId);

                int getNumber = await _dbContext.MaterialRequests
                    .Where(s => s.SiteId == siteId)
                    .OrderByDescending(o => o.MaterialNumber)
                    .Select(m => m.MaterialNumber)
                    .FirstOrDefaultAsync();

                string refNumber;
                int reqNo;

                if (getNumber != 0)
                {
                    reqNo = getNumber + 1;
                    refNumber = $"REQ-{siteCode}-{reqNo:D4}";
                }
                else
                {
                    refNumber = $"REQ-{siteCode}-0001";
                    reqNo = 1;
                }

                var result = new NewRequestData
                {
                    RefNumber = refNumber,
                    ReqNo = reqNo
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNewRequestData");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        public class NewRequestData
        {
            public string RefNumber { get; set; }
            public int ReqNo { get; set; }
        }

        [Authorize]
        [HttpGet("costcodes")]
        public async Task<IActionResult> GetCostCodes()
        {
            try
            {
                var costCodes = await _dbContext.CostCodes.ToListAsync();
                return Ok(costCodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCostCodes");
                return StatusCode(500, "An error occurred while retrieving cost codes.");
            }
        }

        [Authorize]
        [HttpGet("getitems")]
        public async Task<IActionResult> GetItems()
        {
            try
            {
                // Fetch all items if searchTerm is null or empty
                var itemsQuery = _dbContext.Items.AsQueryable();


                var items = await itemsQuery
                    .Select(i => new
                    {
                        ItemId = i.ItemId.ToString(),
                        Text = i.ItemName,
                        ItemUnit = i.ItemUnit,
                        CategoryId = i.CategoryId,
                        //SubCategory = i.SubCategory,
                        Selected = false
                    })
                    .ToListAsync();

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetItems");
                return StatusCode(500, "An error occurred while fetching items.");
            }
        }


        [Authorize]
        [HttpGet("subcontractors")]
        public async Task<IActionResult> GetSubcontractors()
        {
            try
            {
                var subcontractors = await _dbContext.SubContractors
                    .Select(s => new { s.SubId, s.SubName, s.CountryId })
                    .Take(200)
                    .ToListAsync();
                return Ok(subcontractors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSubcontractors");
                return StatusCode(500, "An error occurred while retrieving subcontractors.");
            }
        }

        [Authorize]
        [HttpPost("createnewrequest/{siteId}")]
        public async Task<IActionResult> CreateNewMaterialRequest(int siteId, [FromBody] NewMaterialRequestModel model)
        {
            try
            {
                // Log request entry
                _logger.LogInformation("Received a request to create a new material request for siteId: {siteId}", siteId);

                // Validate User
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogWarning("Unauthorized access attempt with missing or invalid user claim.");
                    return Unauthorized();
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {userId} not found.", userId);
                    return NotFound("User not found.");
                }

                if (!CanUserSendRequests(user.RoleId))
                {
                    _logger.LogWarning("User with ID {userId} does not have permission to send requests.", userId);
                    return Forbid("User does not have permission to send requests.");
                }

                if (!await UserHasAccessToSite(userId, siteId))
                {
                    _logger.LogWarning("User with ID {userId} does not have access to siteId {siteId}.", userId, siteId);
                    return Forbid("User does not have access to the specified site.");
                }

                // Validate Site and Country
                string siteCode = _common.GetSiteCode(siteId);
                var site = await _dbContext.Sites.FirstOrDefaultAsync(s => s.SiteId == siteId);
                if (site == null)
                {
                    _logger.LogWarning("Site with ID {siteId} not found.", siteId);
                    return NotFound("Site not found.");
                }

                var country = await _dbContext.Countries.FirstOrDefaultAsync(c => c.CountryId == site.CountryId);
                if (country == null)
                {
                    _logger.LogWarning("Country with ID {countryId} not found.", site.CountryId);
                    return NotFound("Country not found.");
                }

                // Generate Reference Number
                int latestRequestNumber = await _dbContext.MaterialRequests
                    .Where(m => m.SiteId == siteId)
                    .OrderByDescending(o => o.MaterialId)
                    .Select(m => m.MaterialNumber)
                    .FirstOrDefaultAsync();

                int newRequestNumber = latestRequestNumber + 1;
                string refNumber = $"REQ-{siteCode}-{newRequestNumber:D4}-{country.CountryCode}";

                _logger.LogInformation("Generated reference number: {refNumber}", refNumber);

                // Create Material Request
                bool isPmRole = user.RoleId == 7 || user.RoleId == 10;
                var newRequest = new MaterialRequest
                {
                    MaterialNumber = newRequestNumber,
                    RefNo = refNumber,
                    SiteId = siteId,
                    Date = DateTime.Now,
                    Status = "Pending Approval",
                    Remarks = model.Remarks,
                    UsrId = userId,
                    IsApprovedByPm = isPmRole
                };

                _dbContext.MaterialRequests.Add(newRequest);
                await _dbContext.SaveChangesAsync();

                // Validate Items - Use Contains with plain integer list
                var itemIds = model.Items.Select(i => i.ItemId).ToList();

                var items = await _dbContext.Items
                    .Where(i => itemIds.Contains(i.ItemId)) // EF Core translates this to SQL IN clause
                    .Select(i => new { i.ItemId, i.CategoryId })
                    .ToListAsync();

                if (items.Count != itemIds.Count)
                {
                    var missingItemIds = itemIds.Except(items.Select(i => i.ItemId)).ToList();
                    _logger.LogWarning("Invalid Item IDs found: {MissingItemIds}", missingItemIds);
                    return BadRequest($"One or more Item IDs are invalid: {string.Join(", ", missingItemIds)}");
                }

                // Add Material Details
                foreach (var item in model.Items)
                {
                    var categoryId = items.First(i => i.ItemId == item.ItemId).CategoryId;
                    var newDetail = new MaterialDetail
                    {
                        MaterialId = newRequest.MaterialId,
                        ItemId = item.ItemId,
                        Quantity = item.Quantity,
                        CodeId = item.CostCodeId,
                        SubId = item.SubId,
                        SiteId = siteId,
                        CategoryId = categoryId,
                        UsrId = userId
                    };

                    _dbContext.MaterialDetails.Add(newDetail);
                }

                await _dbContext.SaveChangesAsync();

                // Cleanup Temporary Data
                await DeleteTemMaterialRequestItemsAsync(userId, siteId);

                // Notify Project Managers
                var projectManagers = await _dbContext.Users
                    .Where(u => u.RoleId == 7 && u.SiteId == siteId)
                    .ToListAsync();

                foreach (var pm in projectManagers)
                {
                    _logger.LogInformation("Notifying project manager with ID: {pmId}", pm.UsrId);
                    // Implement notification logic here
                }

                return Ok(new { message = "Material request created successfully", requestId = newRequest.MaterialId, refNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateNewMaterialRequest");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }


        [Authorize]
        [HttpGet("listrequests/{siteId}")]
        public async Task<IActionResult> ListRequests(int siteId, [FromQuery] string status = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                if (!await UserHasAccessToSite(userId, siteId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "User does not have access to the specified site.");
                }

                var query = _dbContext.MaterialRequests.Where(r => r.SiteId == siteId);

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(r => r.Status == status);
                }

                var requests = await query
                    .OrderByDescending(r => r.Date)
                    .Select(r => new
                    {
                        r.MaterialId,
                        r.MaterialNumber,
                        r.RefNo,
                        r.Status,
                        r.Date,
                        r.IsApprovedByPm,
                        r.Remarks
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ListRequests");
                return StatusCode(500, "An error occurred while retrieving requests.");
            }
        }


        [Authorize]
        [HttpGet("requestdetails/{materialId}")]
        public async Task<IActionResult> GetRequestDetails(int materialId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var request = await _dbContext.MaterialRequests
                    .FirstOrDefaultAsync(r => r.MaterialId == materialId);

                if (request == null)
                {
                    return NotFound("Request not found.");
                }

                if (!await UserHasAccessToSite(userId, request.SiteId))
                {
                    return Forbid("User does not have access to this request.");
                }

                var details = await _dbContext.MaterialDetails
                    .Where(d => d.MaterialId == materialId)
                    .Select(d => new
                    {
                        d.MaterialDetailId,
                        d.Quantity,
                        d.SubId,
                        d.CategoryId,
                        d.ItemId,
                        d.CodeId,
                        d.GetItems.ItemName,
                        d.GetItems.ItemUnit,
                        d.GetCost_Codes.Code,
                    })
                    .ToListAsync();

                var result = new
                {
                    Request = new
                    {
                        request.MaterialId,
                        request.MaterialNumber,
                        request.RefNo,
                        request.RejectionNote,
                        request.Status,
                        request.SiteId,
                        request.UsrId,
                        request.Date,
                        request.IsApprovedByPm,
                        request.Remarks
                    },
                    Details = details
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRequestDetails");
                return StatusCode(500, "An error occurred while retrieving request details.");
            }
        }

        private async Task<bool> UserHasAccessToSite(int userId, int siteId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return false;
            }

            switch (user.RoleId)
            {
                case 1: // Admin
                    return true; // Admin has access to all sites

                case 2:
                case 3:
                case 6:
                case 8:
                case 9:
                    // These roles have access to all sites within their assigned countries
                    // Including additional countries from UserCountries

                    // Get all accessible country IDs for the user
                    var accessibleCountryIds = await GetAccessibleCountryIdsAsync(user);

                    // Check if the site belongs to any of the accessible countries
                    var siteCountryId = await _dbContext.Sites
                        .Where(s => s.SiteId == siteId)
                        .Select(s => s.CountryId)
                        .FirstOrDefaultAsync();

                    return accessibleCountryIds.Contains(siteCountryId);

                case 4:
                case 5:
                case 7:
                case 10:
                    // These roles have access to specific sites and may have additional sites from UserSites

                    // Check if the site is the user's primary site
                    if (user.SiteId == siteId)
                    {
                        return true;
                    }

                    // Check if the site is in UserSites
                    return await _dbContext.UserSites
                        .AnyAsync(us => us.UsrId == userId && us.SiteId == siteId);

                default:
                    return false; // Other roles don't have access to send requests
            }
        }

        [Authorize]
        [HttpPost("approve/{materialId}")]
        public async Task<IActionResult> ApproveRequest(int materialId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                var request = await _dbContext.MaterialRequests.FindAsync(materialId);
                if (request == null)
                {
                    return NotFound("Request not found.");
                }

                if (!await UserHasAccessToSite(userId, request.SiteId))
                {
                    // Return 403 Forbidden with a custom message
                    return StatusCode(StatusCodes.Status403Forbidden, "User does not have access to this site.");
                }

                if (user.RoleId == 7 && request.Status == "Pending Approval" && request.IsApprovedByPm == false)
                {
                    // Approve by RoleId 7"
                    request.IsApprovedByPm = true;
                }
                else if (user.RoleId == 3 && request.Status == "Pending Approval" && request.IsApprovedByPm == true)
                {
                    // Approve by RoleId 3: Change status to "PO in Progress"
                    request.Status = "PO in Progress";
                }
                else
                {
                    return BadRequest("Invalid role or request status for approval.");
                }

                _dbContext.MaterialRequests.Update(request);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Request approved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ApproveRequest");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        [Authorize]
        [HttpGet("notifications/{siteId}")]
        public async Task<IActionResult> PopulateNotifications(int siteId)
        {
            try
            {
                // 1. Validate the current user
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                // 2. Check if user has access to the requested site, if necessary
                //    (Uncomment the lines below if you wish to enforce site-access checks)
                /*
                if (!await UserHasAccessToSite(user.UsrId, siteId))
                {
                    return Forbid("User does not have access to this site.");
                }
                */

                // 3. Build the query based on the user's role
                IQueryable<MaterialRequest> query = _dbContext.MaterialRequests
                    .Where(m => m.SiteId == siteId);

                if (user.RoleId == 7 || user.RoleId == 10)
                {
                    // Show requests that have not been approved by PM
                    query = query.Where(r => r.IsApprovedByPm == false);
                }
                else if (user.RoleId == 3)
                {
                    // Show requests that are approved by PM and are Pending Approval
                    query = query.Where(r => r.IsApprovedByPm == true && r.Status == "Pending Approval");
                }
                else
                {
                    // If the user doesn't match the roles above, return an empty list or a 403
                    return Ok(new List<string>());
                }

                // 4. Execute the query
                var requests = await query.ToListAsync();

                // If there are no matching requests, return an empty list
                if (!requests.Any())
                {
                    return Ok(new List<string>());
                }

                // 5. Fetch the users who created these requests
                var creatorIds = requests.Select(r => r.UsrId).Distinct().ToList();
                var creators = await _dbContext.Users
                    .Where(u => creatorIds.Contains(u.UsrId))
                    .Select(u => new { u.UsrId, u.UserName })
                    .ToListAsync();

                // Make a map: UserId -> UserName
                var userMap = creators.ToDictionary(u => u.UsrId, u => u.UserName);

                // 6. Construct notifications
                var notifications = requests.Select(r =>
                {
                    int key = r.UsrId ?? 0;  // 0 is the default if r.UsrId is null
                    string creatorName = userMap.ContainsKey(key) ? userMap[key] : "Unknown User";


                    return new
                    {
                        Message = $"{creatorName} has generated the request number {r.RefNo} that needs your attention."
                    };
                });

                // 7. Return the result
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PopulateNotifications");
                return StatusCode(500, "An error occurred while populating notifications.");
            }
        }


        [Authorize]
        [HttpPost("reject/{materialId}")]
        public async Task<IActionResult> RejectRequest(int materialId, [FromBody] RejectRequestDto rejectRequestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                var request = await _dbContext.MaterialRequests.FindAsync(materialId);
                if (request == null)
                {
                    return NotFound("Request not found.");
                }

                if (!await UserHasAccessToSite(userId, request.SiteId))
                {
                    // Return 403 Forbidden with a custom message
                    return StatusCode(StatusCodes.Status403Forbidden, "User does not have access to this site.");
                }

                // Determine if the user has the authority to reject based on RoleId and Status
                bool canReject = (user.RoleId == 7 && request.Status == "Pending Approval") ||
                                 (user.RoleId == 3 && request.Status == "Pending POs");

                if (canReject)
                {
                    // Construct the rejection note
                    string formattedRejectionNote = $"Rejected by {user.UserName} because of: {rejectRequestDto.RejectionNote}";

                    // Update the MaterialRequest entity
                    request.Status = "Rejected";
                    request.RejectionNote = formattedRejectionNote;
                    request.UsrId = userId; // Assuming UsrId tracks the user who made the last update

                    _dbContext.MaterialRequests.Update(request);
                    await _dbContext.SaveChangesAsync();

                    return Ok(new { message = "Request rejected successfully." });
                }
                else
                {
                    return BadRequest("Invalid role or request status for rejection.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RejectRequest");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        [Authorize]
        [HttpPut("edit/{materialId}")]
        public async Task<IActionResult> EditMaterialRequest(int materialId, [FromBody] UpdateMaterialRequestModel model)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                if (!CanUserSendRequests(user.RoleId))
                {
                    return Forbid("User does not have permission to edit requests.");
                }

                var request = await _dbContext.MaterialRequests
                    .Include(r => r.MaterialDetails)
                    .FirstOrDefaultAsync(r => r.MaterialId == materialId);

                if (request == null)
                {
                    return NotFound("Request not found.");
                }

                if (!await UserHasAccessToSite(userId, request.SiteId))
                {
                    return Forbid("User does not have access to this site.");
                }

                if (request.Status != "Pending Approval" && request.Status != "Rejected")
                {
                    return BadRequest("Only requests with status 'Pending Approval' or 'Rejected' can be edited.");
                }

                // Update request fields
                request.Remarks = model.Remarks ?? request.Remarks;
                request.Status = "Pending Approval"; // Reset status if editing a rejected request
                request.IsApprovedByPm = false;

                // Update material details if provided
                if (model.Items != null && model.Items.Any())
                {
                    // Remove existing details
                    _dbContext.MaterialDetails.RemoveRange(request.MaterialDetails);

                    // Add updated details
                    var itemIds = model.Items.Select(i => i.ItemId).ToList();
                    var items = await _dbContext.Items
                        .Where(i => itemIds.Contains(i.ItemId))
                        .Select(i => new { i.ItemId, i.CategoryId })
                        .ToListAsync();

                    var itemCategoryMap = items.ToDictionary(i => i.ItemId, i => i.CategoryId);

                    foreach (var item in model.Items)
                    {
                        if (!itemCategoryMap.TryGetValue(item.ItemId, out int? categoryId))
                        {
                            return BadRequest($"Invalid ItemId: {item.ItemId}");
                        }

                        var newDetail = new MaterialDetail
                        {
                            MaterialId = request.MaterialId,
                            ItemId = item.ItemId,
                            Quantity = item.Quantity,
                            CodeId = item.CostCodeId,
                            SubId = item.SubId,
                            SiteId = request.SiteId,
                            CategoryId = categoryId.Value,
                            UsrId = userId
                        };

                        _dbContext.MaterialDetails.Add(newDetail);
                    }
                }

                _dbContext.MaterialRequests.Update(request);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Material request updated successfully.", requestId = request.MaterialId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EditMaterialRequest");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        private async Task<List<int>> GetAccessibleCountryIdsAsync(User user)
        {
            var countryIds = new List<int>();

            // Add primary CountryId if it's set
            if (user.CountryId != 0)
            {
                countryIds.Add(user.CountryId);
            }

            // Add additional countries from UserCountries
            var additionalCountryIds = await _dbContext.UserCountries
                .Where(uc => uc.UsrId == user.UsrId)
                .Select(uc => uc.CountryId)
                .ToListAsync();

            countryIds.AddRange(additionalCountryIds);

            return countryIds.Distinct().ToList();
        }

        private bool CanUserSendRequests(int roleId)
        {
            return roleId == 4 || roleId == 5 || roleId == 7 || roleId == 10;
        }

        private async Task DeleteTemMaterialRequestItemsAsync(int userId, int siteId)
        {
            try
            {
                var getData = await _dbContext.MaterialTemps
                   .Where(t => t.SiteId == siteId && t.UsrId == userId)
                   .ToListAsync();

                if (getData.Any())
                {
                    _dbContext.MaterialTemps.RemoveRange(getData);
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteTemMaterialRequestItemsAsync");
            }
        }

        [Authorize]
        [HttpGet("materialrequest/pdf/{id}")]
        public async Task<IActionResult> GetMaterialRequestPdf(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized();
                }

                var getData = await _dbContext.MaterialRequests
                    .Where(m => m.MaterialId == id)
                    .Include(i => i.GetSites)
                    .FirstOrDefaultAsync();

                if (getData == null)
                {
                    return NotFound("Material request not found.");
                }

                if (!await UserHasAccessToSite(userId, getData.SiteId))
                {
                    return Forbid("User does not have access to this material request.");
                }

                var getMaterialDetail = await _dbContext.MaterialDetails
                    .Where(m => m.MaterialId == id)
                    .Include(m => m.GetSites)
                    .Include(m => m.GetItems)
                    .Include(m => m.GetCost_Codes)
                    .ToListAsync();

                string path = string.Empty;
                bool isBigFile = false;
                bool isBigFile2 = false;
                if (getMaterialDetail.Count > 24)
                {
                    path = Path.Combine(_hostingEnvironment.WebRootPath, "Files/RDP2.xlsx");
                    isBigFile2 = true;
                }
                else if (getMaterialDetail.Count > 13)
                {
                    path = Path.Combine(_hostingEnvironment.WebRootPath, "Files/RDP1.xlsx");
                    isBigFile = true;
                }
                else
                {
                    path = Path.Combine(_hostingEnvironment.WebRootPath, "Files/RDP.xlsx");
                }

                string qrcodestring = $"https://pam.karamentreprises.com/Sites/Download_MaterialRequest?id={id}";
                string qrCodeImagePath = QrGenerator(qrcodestring);
                if (!System.IO.File.Exists(qrCodeImagePath))
                {
                    return StatusCode(500, "Error generating QR code.");
                }

                using (var excelEngine = new ExcelEngine())
                {
                    IApplication application = excelEngine.Excel;
                    application.DefaultVersion = ExcelVersion.Excel2016;
                    using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        IWorkbook workbook = application.Workbooks.Open(fileStream);
                        IWorksheet worksheet = workbook.Worksheets["MaterialRequistionForm"];

                        // Read the QR code image into a byte array
                        byte[] qrCodeImageBytes = System.IO.File.ReadAllBytes(qrCodeImagePath);

                        // Add the QR code to the worksheet
                        AddQRCodeToWorksheet(worksheet, qrCodeImageBytes);

                        string str = getData.RefNo;
                        string result = str.Substring(str.LastIndexOf('-') + 1);

                        worksheet.Range["D1:E1"].Text = "REQ-" + getData.GetSites.SiteCode + "-" + result;
                        worksheet.Range["D4:E4"].Text = getData.GetSites.Acronym;
                        worksheet.Range["P1:Q1"].Text = getData.Date.ToString("dd/MM/yyyy");

                        if (isBigFile)
                        {
                            worksheet.Range["E34"].Text = getData.Remarks;
                        }
                        else if (isBigFile2)
                        {
                            worksheet.Range["E62"].Text = getData.Remarks;
                        }
                        else
                        {
                            worksheet.Range["E23"].Text = getData.Remarks;
                        }

                        // For Items
                        int rowStart = 9;
                        if (getMaterialDetail.Count > 0)
                        {
                            foreach (MaterialDetail i in getMaterialDetail)
                            {
                                worksheet.Range["B" + rowStart].Text = i.GetCost_Codes.Code;
                                worksheet.Range["D" + rowStart].Text = i.GetItems.ItemUnit;
                                worksheet.Range["E" + rowStart].Number = i.Quantity;
                                string des = i.GetItems.ItemName;
                                worksheet.Range["F" + rowStart].Text = des;
                                rowStart++;
                            }
                        }

                        using (MemoryStream stream = new MemoryStream())
                        {
                            workbook.SaveAs(stream);

                            XlsIORenderer renderer = new XlsIORenderer();
                            XlsIORendererSettings settings = new XlsIORendererSettings
                            {
                                IsConvertBlankPage = false,
                                DisplayGridLines = GridLinesDisplayStyle.Invisible
                            };

                            stream.Position = 0;
                            using (MemoryStream pdfStream = new MemoryStream())
                            {
                                using (PdfDocument pdfDocument = renderer.ConvertToPDF(workbook, settings))
                                {
                                    pdfDocument.Save(pdfStream);
                                }

                                pdfStream.Position = 0;
                                string pdfFileName = $"{getData.RefNo}.pdf";
                                return File(pdfStream.ToArray(), "application/pdf", pdfFileName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for material request");
                return StatusCode(500, "An error occurred while generating the PDF.");
            }
        }
        private string QrGenerator(string qrText)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(20);

            // Load the logo image
            string logoPath = Path.Combine("wwwroot", "images", "logo.png"); // Ensure this path is correct
            byte[] logoBytes = System.IO.File.ReadAllBytes(logoPath);

            // Combine QR code and logo
            byte[] combinedImage = CombineQrCodeAndLogo(qrCodeImage, logoBytes);

            // Save the combined image to a temporary file
            string tempFile = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), ".png"));
            System.IO.File.WriteAllBytes(tempFile, combinedImage); // Explicit namespace

            return tempFile;
        }

        private byte[] CombineQrCodeAndLogo(byte[] qrCodeImage, byte[] logoImage)
        {
            // Decode the QR code and logo images
            using var qrCodeBitmap = SKBitmap.Decode(qrCodeImage);
            using var logoBitmap = SKBitmap.Decode(logoImage);

            // Resize and position the logo
            int logoSize = qrCodeBitmap.Width / 5;
            int logoX = (qrCodeBitmap.Width - logoSize) / 2;
            int logoY = (qrCodeBitmap.Height - logoSize) / 2;

            // Create a surface to draw the combined image
            using var surface = SKSurface.Create(new SKImageInfo(qrCodeBitmap.Width, qrCodeBitmap.Height));
            var canvas = surface.Canvas;

            // Draw the QR code onto the canvas
            canvas.DrawBitmap(qrCodeBitmap, 0, 0);

            // Draw the resized logo onto the canvas
            using var resizedLogo = logoBitmap.Resize(new SKImageInfo(logoSize, logoSize), SKFilterQuality.High);
            if (resizedLogo != null)
            {
                canvas.DrawBitmap(resizedLogo, new SKRect(logoX, logoY, logoX + logoSize, logoY + logoSize));
            }

            // Get the final combined image as a byte array
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        private void AddQRCodeToWorksheet(IWorksheet worksheet, byte[] qrCodeImageBytes)
        {
            // Load the QR code image from the byte array using SkiaSharp
            using var qrCodeBitmap = SKBitmap.Decode(qrCodeImageBytes);

            // Resize the QR code image
            int newWidth = qrCodeBitmap.Width / 13;
            int newHeight = qrCodeBitmap.Height / 13;
            using var resizedImage = new SKBitmap(newWidth, newHeight);
            using (var canvas = new SKCanvas(resizedImage))
            {
                canvas.DrawBitmap(qrCodeBitmap, new SKRect(0, 0, newWidth, newHeight));
            }

            // Encode the resized image into a byte array
            using var resizedImageStream = new MemoryStream();
            using (var data = resizedImage.Encode(SKEncodedImageFormat.Png, 100))
            {
                data.SaveTo(resizedImageStream);
            }

            resizedImageStream.Position = 0;

            // Convert to Syncfusion.Drawing.Image for adding to worksheet
            var syncfusionImage = Syncfusion.Drawing.Image.FromStream(resizedImageStream);

            worksheet.PageSetup.CenterHeaderImage = syncfusionImage;
            worksheet.PageSetup.CenterHeader = "&G";
        }
        public class UpdateMaterialRequestModel
        {
            public string Remarks { get; set; }
            public List<MaterialRequestItemModel> Items { get; set; }
        }
        public class NewMaterialRequestModel
        {
            public string Remarks { get; set; }
            public List<MaterialRequestItemModel> Items { get; set; }
        }
        public class RejectRequestDto
        {
            [Required]
            [StringLength(500, ErrorMessage = "Rejection note cannot exceed 500 characters.")]
            public string RejectionNote { get; set; }
        }
        public class MaterialRequestItemModel
        {
            public int ItemId { get; set; }
            public double Quantity { get; set; }
            public int CostCodeId { get; set; }
            public int SubId { get; set; }
        }
    }
}
