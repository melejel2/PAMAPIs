﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc.Rendering;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Drawing;
using System.Text;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using PAMAPIs.Models;
using PAMAPIs.Services;
using PAMAPIs.Data;

using Syncfusion.Pdf;
using Syncfusion.XlsIO;
using Syncfusion.XlsIORenderer;

using QRCoder;


namespace PAM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SitesApiController : ControllerBase
    {
        private readonly FuzzyMatchingService _fuzzyMatchingService;
        private readonly PAMContext _dbContext;
        private readonly Common _common;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<SitesApiController> _logger;
        private readonly IConfiguration _configuration;

        public SitesApiController(
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
            _logger = loggerFactory.CreateLogger<SitesApiController>();
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserEmail == model.Email);
                if (user == null)
                {
                    return Unauthorized("Invalid username.");
                }

                if (user.UserPassword != model.Password)
                {
                    return Unauthorized("Invalid password.");
                }

                var token = GenerateJwtToken(user);

                // Update last login time
                user.LastLogin = DateTime.Now;
                _dbContext.Entry(user).State = EntityState.Modified;
                await _dbContext.SaveChangesAsync();

                if (!user.UpdatePass)
                {
                    return Ok(new { requirePasswordUpdate = true, token });
                }

                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login");
                return StatusCode(500, "An error occurred. Please try again.");
            }
        }

        [Authorize]
        [HttpGet("usercountries")]
        public async Task<IActionResult> GetUserCountries()
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

                var countries = await GetUserCountriesAsync(user);
                return Ok(countries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserCountries");
                return StatusCode(500, "An error occurred while retrieving user countries.");
            }
        }

        [Authorize]
        [HttpGet("usersites")]
        public async Task<IActionResult> GetUserSites()
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

                var sites = await GetUserSitesAsync(user);
                return Ok(sites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserSites");
                return StatusCode(500, "An error occurred while retrieving user sites.");
            }
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
        [HttpGet("subcontractors/{siteId}")]
        public async Task<IActionResult> GetSubcontractors(int siteId)
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

                // Verify if the user has access to the specified site
                if (!await UserHasAccessToSite(userId, siteId))
                {
                    return Forbid("User does not have access to the specified site.");
                }

                var site = await _dbContext.Sites.FindAsync(siteId);
                if (site == null)
                {
                    return NotFound("Site not found.");
                }

                var subcontractors = await PopulateSubAsync(site.CountryId);
                return Ok(subcontractors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSubcontractors");
                return StatusCode(500, "An error occurred while retrieving subcontractors.");
            }
        }

        [Authorize]
        [HttpGet("searchitems")]
        public async Task<IActionResult> SearchItems([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest("Search term is required.");
                }

                var items = await _dbContext.Items
                    .Where(i => i.ItemName.Contains(searchTerm))
                    .Take(20)
                    .Select(i => new SelectListItem { Value = i.ItemId.ToString(), Text = i.ItemName })
                    .ToListAsync();

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchItems");
                return StatusCode(500, "An error occurred while searching items.");
            }
        }

        [Authorize]
        [HttpPost("createnewrequest/{siteId}")]
        public async Task<IActionResult> CreateNewMaterialRequest(int siteId, [FromBody] NewMaterialRequestModel model)
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

                string siteCode = _common.GetSiteCode(siteId);

                int latestRequestNumber = await _dbContext.MaterialRequests
                    .Where(m => m.SiteId == siteId)
                    .OrderByDescending(o => o.MaterialId)
                    .Select(m => m.MaterialNumber)
                    .FirstOrDefaultAsync();

                int newRequestNumber = latestRequestNumber + 1;
                string refNumber = $"REQ-{siteCode}-{newRequestNumber:D4}";

                var newRequest = new MaterialRequest
                {
                    MaterialNumber = newRequestNumber,
                    RefNo = refNumber,
                    SiteId = siteId,
                    Date = DateTime.Now,
                    Status = "Pending",
                    Remarks = model.Remarks,
                    UsrId = userId,
                    IsApprovedByPm = false
                };

                _dbContext.MaterialRequests.Add(newRequest);
                await _dbContext.SaveChangesAsync();

                foreach (var item in model.Items)
                {
                    var newDetail = new MaterialDetail
                    {
                        MaterialId = newRequest.MaterialId,
                        ItemId = item.ItemId,
                        Quantity = item.Quantity,
                        CodeId = item.CostCodeId,
                        SiteId = siteId
                    };

                    _dbContext.MaterialDetails.Add(newDetail);
                }

                await _dbContext.SaveChangesAsync();

                await DeleteTemMaterialRequestItemsAsync(userId, siteId);

                var projectManagers = await _dbContext.Users
                    .Where(u => u.RoleId == 7 && u.SiteId == siteId)
                    .ToListAsync();

                if (projectManagers.Any())
                {
                    foreach (var pm in projectManagers)
                    {
                        // Implement your notification logic here
                    }
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
                    return Forbid("User does not have access to the specified site.");
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
                        d.CodeId
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

                        AddQRCodeToWorksheet(worksheet, qrCodeImagePath);

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
            using QRCodeGenerator qrGenerator = new();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new(qrCodeData);
            Bitmap qrCodeImage = qrCode.GetGraphic(20);

            // Load the logo image
            string logoPath = Path.Combine("wwwroot", "images", "logo.png"); // Ensure this path is correct
            Bitmap logo = new(logoPath);

            // Calculate the position and size for the logo
            int logoSize = qrCodeImage.Width / 5; // Adjust the size of the logo (e.g., 1/5th of the QR code size)
            int logoX = (qrCodeImage.Width - logoSize) / 2;
            int logoY = (qrCodeImage.Height - logoSize) / 2;

            // Resize the logo
            Bitmap resizedLogo = new(logo, new Size(logoSize, logoSize));

            // Draw the logo onto the QR code
            using (Graphics graphics = Graphics.FromImage(qrCodeImage))
            {
                graphics.DrawImage(resizedLogo, logoX, logoY, logoSize, logoSize);
            }

            // Save the image to a temporary file in the user's temp folder with .png extension
            string tempFile = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), ".png"));
            qrCodeImage.Save(tempFile, System.Drawing.Imaging.ImageFormat.Png);

            // Dispose the logo image to free up resources
            logo.Dispose();
            resizedLogo.Dispose();

            // Return the file path
            return tempFile;
        }
        private void AddQRCodeToWorksheet(IWorksheet worksheet, string qrCodeImagePath)
        {
            using (FileStream imageStream = new FileStream(qrCodeImagePath, FileMode.Open, FileAccess.Read))
            using (var originalImage = System.Drawing.Image.FromStream(imageStream))
            {
                int newWidth = originalImage.Width / 13;
                int newHeight = originalImage.Height / 13;

                var resizedImage = new Bitmap(newWidth, newHeight);
                using (var graphics = Graphics.FromImage(resizedImage))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
                }

                using (var memoryStream = new MemoryStream())
                {
                    resizedImage.Save(memoryStream, ImageFormat.Png);
                    memoryStream.Position = 0;
                    Syncfusion.Drawing.Image syncfusionImage = Syncfusion.Drawing.Image.FromStream(memoryStream);

                    worksheet.PageSetup.CenterHeaderImage = syncfusionImage;
                    worksheet.PageSetup.CenterHeader = "&G";
                }
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
                    return true; // Admin has access to all sites, but can't send requests

                case 4: // Site User
                case 5: // Warehouse Manager
                case 7: // Project Manager
                case 8: // Operations Manager
                        // These roles have access to specific sites and can send requests
                    if (user.SiteId == siteId) // User's default site
                    {
                        return true;
                    }

                    // Check UserSites table for additional site access
                    return await _dbContext.UserSites
                        .AnyAsync(us => us.UsrId == userId && us.SiteId == siteId);

                default:
                    return false; // Other roles don't have access to send requests
            }
        }

        private bool CanUserSendRequests(int roleId)
        {
            return roleId == 4 || roleId == 5 || roleId == 7 || roleId == 8;
        }

        private async Task<List<Country>> GetUserCountriesAsync(User user)
        {
            switch (user.RoleId)
            {
                case 1: // Admin
                    return await _dbContext.Countries.ToListAsync();
                case 2:
                case 3:
                case 6:
                case 8:
                case 9:
                    return await _dbContext.UserCountries
                        .Where(uc => uc.UsrId == user.UsrId)
                        .Join(_dbContext.Countries,
                            uc => uc.CountryId,
                            c => c.CountryId,
                            (uc, c) => c)
                        .ToListAsync();
                case 4:
                case 5:
                case 7:
                    var countriesFromSites = await _dbContext.UserSites
                        .Where(us => us.UsrId == user.UsrId)
                        .Join(_dbContext.Sites,
                            us => us.SiteId,
                            s => s.SiteId,
                            (us, s) => s.CountryId)
                        .Distinct()
                        .Join(_dbContext.Countries,
                            cId => cId,
                            c => c.CountryId,
                            (cId, c) => c)
                        .ToListAsync();

                    if (user.CountryId != 0)
                    {
                        var userCountry = await _dbContext.Countries.FindAsync(user.CountryId);
                        if (userCountry != null && !countriesFromSites.Any(c => c.CountryId == user.CountryId))
                        {
                            countriesFromSites.Add(userCountry);
                        }
                    }

                    return countriesFromSites;
                default:
                    return new List<Country>();
            }
        }

        private async Task<List<Site>> GetUserSitesAsync(User user)
        {
            switch (user.RoleId)
            {
                case 1: // Admin
                    return await _dbContext.Sites.ToListAsync();
                case 2:
                case 3:
                case 6:
                case 8:
                case 9:
                    var countries = await GetUserCountriesAsync(user);
                    return await _dbContext.Sites
                        .Where(s => countries.Select(c => c.CountryId).Contains(s.CountryId))
                        .ToListAsync();
                case 4:
                case 5:
                case 7:
                    var userSites = await _dbContext.UserSites
                        .Where(us => us.UsrId == user.UsrId)
                        .Join(_dbContext.Sites,
                            us => us.SiteId,
                            s => s.SiteId,
                            (us, s) => s)
                        .ToListAsync();

                    if (user.SiteId != 0)
                    {
                        var userSite = await _dbContext.Sites.FindAsync(user.SiteId);
                        if (userSite != null && !userSites.Any(s => s.SiteId == user.SiteId))
                        {
                            userSites.Add(userSite);
                        }
                    }

                    return userSites;
                default:
                    return new List<Site>();
            }
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

        private async Task<List<SelectListItem>> PopulateSubAsync(int countryId)
        {
            try
            {
                var subcontractors = await _dbContext.SubContractors
                    .Where(c => c.SubName != "Returned to Supplier" && (c.CountryId == countryId || c.CountryId == 0))
                    .OrderBy(s => s.SubName)
                    .Select(s => new SelectListItem { Value = s.SubId.ToString(), Text = s.SubName })
                    .ToListAsync();

                return subcontractors;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PopulateSubAsync");
                return new List<SelectListItem>();
            }
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];

            if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer))
            {
                throw new InvalidOperationException("JWT configuration is missing or invalid.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UsrId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.RoleId.ToString()),
                new Claim("CountryId", user.CountryId.ToString()),
                new Claim("SiteId", user.SiteId.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddDays(Convert.ToDouble(_configuration["Jwt:ExpireDays"] ?? "7"));

            var token = new JwtSecurityToken(
                jwtIssuer,
                jwtIssuer,
                claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class LoginModel
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class NewMaterialRequestModel
        {
            public string Remarks { get; set; }
            public List<MaterialRequestItemModel> Items { get; set; }
        }

        public class MaterialRequestItemModel
        {
            public int ItemId { get; set; }
            public double Quantity { get; set; }
            public int CostCodeId { get; set; }
        }
    }
}