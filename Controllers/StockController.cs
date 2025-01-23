// Controllers/StockController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PAM.Models.EntityModels;
using PAMAPIs.Data;
using PAMAPIs.Models;
using PAMAPIs.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PAMAPIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly PAMContext _dbContext;
        private readonly EmailService _emailService;

        public StockController(PAMContext dbContext, EmailService emailService)
        {
            _dbContext = dbContext;
            _emailService = emailService;
        }

        /// <summary>
        /// Retrieves the stock status for a specific site (existing logic).
        /// GET: api/Stock/GetSiteStockStatus/{siteId}
        /// </summary>
        [HttpGet("GetSiteStockStatus/{siteId}")]
        public async Task<ActionResult<List<StockVM>>> GetSiteStockStatus(int siteId)
        {
            var site = await _dbContext.Sites
                .FirstOrDefaultAsync(s => s.SiteId == siteId && !s.IsDead);

            if (site == null)
                return NotFound(new { Message = "Site not found or is inactive." });

            var data = await (from item in _dbContext.Items
                              join cat in _dbContext.Categories on item.CategoryId equals cat.CategoryId
                              select new StockVM
                              {
                                  ItemId = item.ItemId,
                                  Item = item.ItemName,
                                  Unit = item.ItemUnit,
                                  CategoryName = cat.CategoryName,
                                  SiteName = site.SiteName,
                                  Acronym = site.Acronym,

                                  Requested = _dbContext.MaterialDetails
                                      .Where(md => md.ItemId == item.ItemId && md.SiteId == siteId)
                                      .Sum(md => (double?)md.Quantity) ?? 0,

                                  Ordered = _dbContext.PoDetails
                                      .Where(po => po.ItemId == item.ItemId && po.SiteId == siteId)
                                      .Sum(po => (double?)po.Qty) ?? 0,

                                  Received = (_dbContext.InStocks
                                                  .Where(ins => ins.ItemId == item.ItemId && ins.SiteId == siteId)
                                                  .Sum(ins => (double?)ins.Quantity) ?? 0),

                                  Consumed = (_dbContext.OutStocks
                                                  .Where(outs => outs.ItemId == item.ItemId && outs.SiteId == siteId)
                                                  .Sum(outs => (double?)outs.Quantity) ?? 0)

                              }).ToListAsync();

            // Optionally filter out items where all quantities are zero
            data = data.Where(d => d.Requested != 0 || d.Ordered != 0 || d.Received != 0 || d.Consumed != 0).ToList();

            return Ok(data);
        }

        // ------------------------------------------------------------------------
        // NEW 1: GET UNIT => "GetUnit?val={itemId}"
        // Example to retrieve item unit + available outstock quantity
        // ------------------------------------------------------------------------
        /// <summary>
        /// Gets the unit of a given item plus its available out-stock quantity at the user's site.
        /// E.g., GET: api/Stock/GetUnit?val=123
        /// </summary>
        [HttpGet("GetUnit")]
        public async Task<IActionResult> GetUnit([FromQuery] int val)
        {
            try
            {
                if (val <= 0)
                    return BadRequest(new { success = false, message = "No valid item ID provided." });

                // For simplicity, read user and site from headers:
                int userId, siteId;
                if (!TryGetUserAndSiteFromHeaders(out userId, out siteId))
                    return Unauthorized(new { success = false, message = "UserID and SiteID headers are missing or invalid." });

                var item = await _dbContext.Items.FirstOrDefaultAsync(a => a.ItemId == val);
                if (item == null)
                    return NotFound(new { success = false, message = $"Item ID={val} not found in DB." });

                // Suppose you have some logic to compute the "available stock" for an out-stock operation:
                double availableQty = await ComputeAvailableOutStockQty(val, siteId);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        getData = item.ItemUnit,
                        stock = availableQty
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ------------------------------------------------------------------------
        // NEW 2: GET STOCK OUT STATUS => "GetStockOutStatus/{siteId}"
        // ------------------------------------------------------------------------
        [HttpGet("GetStockOutStatus/{siteId}")]
        public async Task<IActionResult> GetStockOutStatus(int siteId)
        {
            try
            {
                // Example logic: just aggregate from OutStocks
                var data = await _dbContext.OutStocks
                    .Where(o => o.SiteId == siteId)
                    .Select(o => new
                    {
                        Item = o.GetItems.ItemName,
                        Unit = o.GetItems.ItemUnit,
                        Requested = 0,    // or your own calculation
                        Ordered = 0,
                        Received = 0,
                        Consumed = o.Quantity
                    })
                    .ToListAsync();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


        // ------------------------------------------------------------------------
        // NEW 3: POPULATE SITES FOR "OTHER SITE" => "PopulateSitesForOtherSite"
        // Example to retrieve sites in the same country, excluding the current site
        // ------------------------------------------------------------------------
        /// <summary>
        /// Returns a list of sites in the same country as the current site, excluding the current site.
        /// GET: api/Stock/PopulateSitesForOtherSite
        /// </summary>
        [HttpGet("PopulateSitesForOtherSite")]
        public async Task<IActionResult> PopulateSitesForOtherSite()
        {
            try
            {
                int userId, currentSiteId;
                if (!TryGetUserAndSiteFromHeaders(out userId, out currentSiteId))
                    return Unauthorized(new { success = false, message = "UserID and SiteID headers are missing or invalid." });

                var currentSite = await _dbContext.Sites.FindAsync(currentSiteId);
                if (currentSite == null)
                    return NotFound(new { success = false, message = "Current site not found." });

                int countryId = currentSite.CountryId; // If your table has CountryId

                var list = await _dbContext.Sites
                    .Where(s => s.CountryId == countryId && s.SiteId != currentSiteId)
                    .Select(s => new
                    {
                        s.SiteId,
                        s.SiteName
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ------------------------------------------------------------------------
        // NEW 4: POPULATE CONTRACT NUMBERS => "PopulateNum?id={subId}"
        // Example to retrieve SubContractNumbers for a given SubId at the user's site
        // ------------------------------------------------------------------------
        /// <summary>
        /// Returns contract numbers for a given subcontractor at the current site.
        /// GET: api/Stock/PopulateNum?id={subId}
        /// </summary>
        [HttpGet("PopulateNum")]
        public async Task<IActionResult> PopulateNum([FromQuery] int id)
        {
            try
            {
                int userId, siteId;
                if (!TryGetUserAndSiteFromHeaders(out userId, out siteId))
                    return Unauthorized(new { success = false, message = "UserID and SiteID headers are missing or invalid." });

                // Example: if you have a SubContractNumbers table referencing subId + siteId
                var data = await _dbContext.SubContractNumbers
                    .Where(sc => sc.SubId == id && sc.SiteId == siteId)
                    .Select(sc => new
                    {
                        sc.NumId,
                        sc.ContractNumber
                    })
                    .ToListAsync();

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ------------------------------------------------------------------------
        // EXISTING: InStock (unchanged)
        // POST: api/Stock/InStock
        // ------------------------------------------------------------------------
        [HttpPost("InStock")]
        [Authorize]
        public async Task<IActionResult> CreateInStock([FromBody] InStockDto inStockDto)
        {
            if (inStockDto == null)
                return BadRequest(new { Message = "Invalid InStock data." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Retrieve user & site from headers
                if (!Request.Headers.TryGetValue("UserID", out var userIdHeader) ||
                    !Request.Headers.TryGetValue("SiteID", out var siteIdHeader))
                {
                    return Unauthorized(new { Message = "UserID and SiteID headers are missing." });
                }

                if (!int.TryParse(userIdHeader.FirstOrDefault(), out int userId) ||
                    !int.TryParse(siteIdHeader.FirstOrDefault(), out int siteId))
                {
                    return BadRequest(new { Message = "Invalid UserID or SiteID." });
                }

                // Validate PO
                var purchaseOrder = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Poid == inStockDto.POId);
                if (purchaseOrder == null)
                    return BadRequest(new { Message = "No PO record found." });

                // Validate Item
                var item = await _dbContext.Items.FirstOrDefaultAsync(i => i.ItemId == inStockDto.ItemId);
                if (item == null)
                    return BadRequest(new { Message = "No Item record found." });

                // Ensure SiteId is set
                if (inStockDto.SiteId == 0)
                {
                    inStockDto.SiteId = purchaseOrder.SiteId;
                    if (inStockDto.SiteId == 0)
                        return BadRequest(new { Message = "No Site record found for the PO. Please contact support." });
                }

                // Calculate PO quantity with 10% buffer
                var poQty = await _dbContext.PoDetails
                    .Where(p => p.Poid == inStockDto.POId && p.ItemId == inStockDto.ItemId)
                    .SumAsync(s => (double?)s.Qty) ?? 0.0;
                poQty = Math.Round(poQty + poQty * 0.1, 3);

                // Current stock quantity
                var stockQty = await _dbContext.InStocks
                    .Where(p => p.POId == inStockDto.POId && p.ItemId == inStockDto.ItemId)
                    .SumAsync(s => (double?)s.Quantity) ?? 0.0;

                stockQty += inStockDto.Quantity;

                if (stockQty > poQty)
                {
                    return BadRequest(new
                    {
                        Message = $"You cannot do InStock for a purchase order by a quantity that is more than 10% of the initial ordered quantity. Max Ordered Qty = {poQty}"
                    });
                }

                // Assign PODetailId if not provided
                if (inStockDto.PODetailId == 0)
                {
                    inStockDto.PODetailId = await _dbContext.PoDetails
                        .Where(i => i.Poid == inStockDto.POId && i.ItemId == inStockDto.ItemId)
                        .Select(s => (int?)s.PodetailId)
                        .FirstOrDefaultAsync() ?? 0;
                }

                // Assign current date if not provided
                if (inStockDto.Date == null || inStockDto.Date.Value.Year == 1)
                    inStockDto.Date = DateTime.Now;

                    // Normal InStock
                    var inStock = new PAM.Models.EntityModels.InStock
                    {
                        POId = inStockDto.POId,
                        ItemId = inStockDto.ItemId,
                        Quantity = inStockDto.Quantity,
                        PODetailId = inStockDto.PODetailId,
                        UsrId = userId,
                        SiteId = inStockDto.SiteId,
                        RefNo = inStockDto.RefNo,
                        InNo = inStockDto.InNo,
                        Date = inStockDto.Date.Value
                    };
                    _dbContext.InStocks.Add(inStock);

                    // Update StockQuantity
                    var checkItem = await _dbContext.StockQuantities
                        .FirstOrDefaultAsync(i => i.ItemId == inStockDto.ItemId && i.SiteId == inStockDto.SiteId && i.UsrId == userId);

                    if (checkItem != null)
                    {
                        checkItem.QtyReceived += inStockDto.Quantity;
                        checkItem.QtyStock += inStockDto.Quantity;
                        _dbContext.StockQuantities.Update(checkItem);
                    }
                    else
                    {
                        StockQuantity stock = new StockQuantity
                        {
                            SiteId = inStockDto.SiteId,
                            UsrId = userId,
                            ItemId = inStockDto.ItemId,
                            QtyReceived = inStockDto.Quantity,
                            QtyStock = inStockDto.Quantity
                        };
                        _dbContext.StockQuantities.Add(stock);
                    }

                    await _dbContext.SaveChangesAsync();
                

                // Update Purchase Order Status if needed
                await UpdatePurchaseOrderStatusAsync(inStockDto.POId);

                return Ok(new { Message = "InStock operation successful." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"An error occurred: {ex.Message}" });
            }
        }

      [HttpPost("OutStock")]
        [Authorize]
        public async Task<IActionResult> CreateOutStock([FromBody] OutStockRequest dto)
        {
            try
            {
                // 1) Retrieve role, user, site from headers
                if (!Request.Headers.TryGetValue("RoleID", out var roleHeader) ||
                    !Request.Headers.TryGetValue("UserID", out var userIdHeader) ||
                    !Request.Headers.TryGetValue("SiteID", out var siteIdHeader))
                {
                    return Unauthorized(new { success = false, message = "RoleID, UserID, or SiteID headers are missing." });
                }

                if (!int.TryParse(roleHeader.FirstOrDefault(), out int roleId) ||
                    !int.TryParse(userIdHeader.FirstOrDefault(), out int userId) ||
                    !int.TryParse(siteIdHeader.FirstOrDefault(), out int siteId))
                {
                    return BadRequest(new { success = false, message = "Invalid RoleID, UserID, or SiteID header." });
                }

                // 2) Check if role is allowed
                var allowedRolesForStock = new HashSet<int> { 4, 5, 7, 10 };
                if (!allowedRolesForStock.Contains(roleId))
                {
                    return Forbid("You are not allowed to do OutStock.");
                }

                // 3) Basic validations
                if (dto.ItemId == 0)
                {
                    return BadRequest(new { success = false, message = "No item selected." });
                }
                if (dto.Quantity <= 0)
                {
                    return BadRequest(new { success = false, message = "Quantity must be > 0." });
                }
                if (string.IsNullOrWhiteSpace(dto.OutStockNote))
                {
                    return BadRequest(new { success = false, message = "OutStock note is required." });
                }

                // 4) Populate the OutStock entity
                var outStock = new OutStock
                {
                    OutNo = dto.OutNo,
                    RefNo = dto.RefNo,
                    Quantity = dto.Quantity,
                    ItemId = dto.ItemId,
                    SiteId = siteId,
                    UsrId = userId,
                    Date = dto.Date,
                    Remarks = dto.Remarks,
                    OutStockNote = dto.OutStockNote
                };

                // 5) Determine to whom/where it goes
                switch (dto.Search)
                {
                    case "1":
                        // Subcontractor
                        outStock.SubId = dto.SubId;
                        outStock.NumId = dto.NumId;
                        outStock.IsApprovedByOP = true;
                        break;

                    case "3":
                        // Site Consumption
                        outStock.SubId = 1; // or whatever logic you use to denote consumption
                        outStock.NumId = 0;
                        outStock.IsApprovedByOP = true;
                        break;

                    case "4":
                        // Other Site
                        outStock.SubId = 0;
                        outStock.NumId = 0;
                        outStock.ToSiteId = dto.ToSiteId;
                        outStock.IsApprovedByOP = false; 
                        break;

                    default:
                        return BadRequest(new { success = false, message = "Please select a valid 'Out Stock To' option." });
                }

                // 6) Insert new outstock
                _dbContext.OutStocks.Add(outStock);

                // 7) Update stock quantity
                var checkItem = await _dbContext.StockQuantities
                    .FirstOrDefaultAsync(i => i.ItemId == outStock.ItemId && i.SiteId == siteId);
                if (checkItem != null)
                {
                    checkItem.QtyStock -= dto.Quantity;
                    _dbContext.Entry(checkItem).State = EntityState.Modified;
                }
                else
                {
                    return BadRequest(new { success = false, message = "No StockQuantity record found for that item/site." });
                }

                await _dbContext.SaveChangesAsync();

                // 8) Build JSON for the newly created row
                var item = await _dbContext.Items.FirstOrDefaultAsync(x => x.ItemId == outStock.ItemId);
                string itemName = item?.ItemName ?? "";
                string itemUnit = item?.ItemUnit ?? "";

                // E.g., "subName" or "entity"
                string subName = "";
                if (outStock.ToSiteId.HasValue && outStock.ToSiteId.Value != 0)
                {
                    // "Other Site"
                    subName = await _dbContext.Sites
                        .Where(s => s.SiteId == outStock.ToSiteId.Value)
                        .Select(s => s.SiteName)
                        .FirstOrDefaultAsync() ?? "Other Site";
                }
                else if (outStock.SubId != 0)
                {
                    // Subcontractor or site consumption
                    if (outStock.SubId == 1)
                        subName = "Site Consumption";
                    else
                        subName = await _dbContext.SubContractors
                            .Where(s => s.SubId == outStock.SubId)
                            .Select(s => s.SubName)
                            .FirstOrDefaultAsync() ?? "Subcontractor";
                }
                else
                {
                    subName = "Site Consumption";
                }

                string contractNumber = "";
                if (outStock.NumId != 0)
                {
                    contractNumber = await _dbContext.SubContractNumbers
                        .Where(s => s.NumId == outStock.NumId)
                        .Select(s => s.ContractNumber)
                        .FirstOrDefaultAsync() ?? "";
                }

                var newRow = new
                {
                    RefNo = outStock.RefNo,
                    ItemName = itemName,
                    ItemUnit = itemUnit,
                    // Use double quotes with numeric format string:
                    Quantity = outStock.Quantity?.ToString("0.##"),
                    SubName = subName,
                    ContractNumber = contractNumber,
                    DateString = outStock.Date?.ToString("dd-MM-yyyy"),
                    OutId = outStock.OutId
                };

                // 9) If "Other Site" => optional notification
                if (dto.Search == "4" && dto.ToSiteId.HasValue && dto.ToSiteId.Value != 0)
                {
                    await SendOutStockNotificationAsync(outStock);
                }

                // 10) Return success
                return Ok(new
                {
                    success = true,
                    message = "OutStock saved successfully.",
                    newRow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error in OutStock: " + ex.Message
                });
            }
        }        private async Task SendOutStockNotificationAsync(PAM.Models.EntityModels.OutStock outStock)
        {
            try
            {
                // 1) Find the 'from site' (origin) + 'to site' (destination)
                var fromSite = await _dbContext.Sites
                    .FirstOrDefaultAsync(s => s.SiteId == outStock.SiteId);
                var toSite = await _dbContext.Sites
                    .FirstOrDefaultAsync(s => s.SiteId == outStock.ToSiteId);

                // 2) Fetch the item + user who performed the outstock
                var item = await _dbContext.Items
                    .FirstOrDefaultAsync(i => i.ItemId == outStock.ItemId);
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.UsrId == outStock.UsrId);

                if (fromSite == null || toSite == null || item == null || user == null)
                {
                    // If any essential data is missing, just exit
                    return;
                }

                // 3) Attempt to get "Project Managers" = RoleId 7 OR 10 for the *toSite*.
                var pms = await _dbContext.Users
                    .Where(u => u.SiteId == toSite.SiteId && (u.RoleId == 7 || u.RoleId == 10))
                    .ToListAsync();

                // 4) If none found, fallback to role 4
                if (!pms.Any())
                {
                    pms = await _dbContext.Users
                        .Where(u => u.SiteId == toSite.SiteId && u.RoleId == 4)
                        .ToListAsync();
                }

                // If still none found, no one to notify
                if (!pms.Any()) return;

                // 5) Build the TO recipients
                var toEmails = pms
                    .Where(pm => !string.IsNullOrEmpty(pm.UserEmail))
                    .Select(pm => pm.UserEmail + "@seg-int.com")
                    .Distinct()
                    .ToList();

                // 6) The origin site’s country decides the static CC
                var countryId = fromSite.CountryId;
                var ccEmails = new List<string>();
                switch (countryId)
                {
                    case 1:
                        ccEmails.Add("ejabbour@seg-int.com");
                        break;
                    case 2:
                        ccEmails.Add("belaridi@seg-int.com");
                        break;
                    case 3:
                        ccEmails.Add("gkonaizeh@seg-int.com");
                        break;
                    default:
                        // If you have a default ops manager or none, do something here...
                        break;
                }

                // 7) Construct a subject
                string subject = $"Material Transfer: {fromSite.SiteName} -> {toSite.SiteName}";

                // 8) Build the email body (HTML)
                string greeting = (pms.Count == 1)
                    ? $"Dear <b>{pms.First().UserName}</b>"
                    : "Dear Project Managers";

                string messageBody = $@"
<html>
<head>
    <title>OutStock Notification</title>
</head>
<body>
    <h2 style='color: #007BFF;'>Material Transfer Notification</h2>
    <p>{greeting},</p>
    <p>This is to inform you that <b>{user.UserName}</b> from <b>{fromSite.SiteName}</b> 
       has initiated a transfer of <b>{item.ItemName}</b> to <b>{toSite.SiteName}</b>.</p>
    <p><b>Quantity:</b> {outStock.Quantity} {item.ItemUnit}</p>
    <p><b>Reference No:</b> {outStock.RefNo}</p>
    <p><b>Date:</b> {outStock.Date:dd-MM-yyyy}</p>
    {(string.IsNullOrWhiteSpace(outStock.Remarks) ? "" : $"<p><b>Remarks:</b> {outStock.Remarks}</p>")}
    <p>You have to review and receive this material in your stock when it arrives.</p>
    <hr />
    <p>Have a nice day!</p>
</body>
</html>";

                // 9) Send email (assuming you injected an _emailService in this controller).
                await _emailService.SendEmailAsync(toEmails.ToArray(), subject, messageBody, ccEmails.ToArray());

                // 10) (Optional) Log the email send in DB if needed
                // e.g. _dbContext.EmailSendLogs.Add(...)
                // await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error - you might want to use a proper logging framework
                System.Diagnostics.Debug.WriteLine($"Error sending outstock notification: {ex.Message}");
                // Optionally rethrow if you want the caller to handle it
                // throw;
            }
        }

        // ===========================================================================
        // HELPER METHODS
        // ===========================================================================

        /// <summary>
        /// Attempts to read UserID and SiteID from the request headers. 
        /// Returns false if invalid or missing.
        /// </summary>
        private bool TryGetUserAndSiteFromHeaders(out int userId, out int siteId)
        {
            userId = 0;
            siteId = 0;
            if (!Request.Headers.TryGetValue("UserID", out var userIdHeader) ||
                !Request.Headers.TryGetValue("SiteID", out var siteIdHeader))
            {
                return false;
            }

            return (int.TryParse(userIdHeader.FirstOrDefault(), out userId) &&
                    int.TryParse(siteIdHeader.FirstOrDefault(), out siteId));
        }

        /// <summary>
        /// Compute the "available quantity" for outstock. 
        /// Replace with your real logic or a DB-based approach.
        /// </summary>
        private async Task<double> ComputeAvailableOutStockQty(int itemId, int siteId)
        {
            // For example, your "stock on hand" minus "stock reserved" 
            // or reusing your existing StockQuantities table.
            var stockRec = await _dbContext.StockQuantities
                .Where(sq => sq.ItemId == itemId && sq.SiteId == siteId)
                .FirstOrDefaultAsync();
            if (stockRec == null) return 0.0;

            // Possibly, you might subtract out pending outstocks if that’s part of your design
            // This is just a placeholder:
            double usedQty = 0.0;
            double available = (stockRec.QtyStock ?? 0.0) - usedQty;
            return available < 0 ? 0 : available;
        }

        /// <summary>
        /// Determines if the given site is a warehouse site (your existing logic).
        /// </summary>
        private async Task<bool> IsWarehouseSiteAsync(int siteId)
        {
            var site = await _dbContext.Sites.FirstOrDefaultAsync(s => s.SiteId == siteId);
            return site != null;  // Return the boolean result directly
        }

        /// <summary>
        /// Retrieves the Supplier ID based on the Purchase Order ID.
        /// </summary>
        private async Task<int> GetSupplierIdAsync(int poId)
        {
            var purchaseOrder = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Poid == poId);
            return purchaseOrder?.SupId ?? 0;
        }

        /// <summary>
        /// Warehouse in-stock operations (existing logic).
        /// </summary>
        private async Task WarehouseInStockAsync(
            int userId, int siteId, double quantity, int itemId, int supplierId,
            int poId, int podetailId, int inNo, string refNo, string suppDeliveryNote, DateTime date)
        {
            var inStock = new PAM.Models.EntityModels.InStock
            {
                POId = poId,
                ItemId = itemId,
                Quantity = quantity,
                PODetailId = podetailId,
                UsrId = userId,
                SiteId = siteId,
                RefNo = refNo,
                InNo = inNo,
                Date = date
            };
            _dbContext.InStocks.Add(inStock);

            var checkItem = await _dbContext.StockQuantities
                .FirstOrDefaultAsync(i => i.ItemId == itemId && i.SiteId == siteId && i.UsrId == userId);

            if (checkItem != null)
            {
                checkItem.QtyReceived += quantity;
                checkItem.QtyStock += quantity;
                _dbContext.StockQuantities.Update(checkItem);
            }
            else
            {
                StockQuantity stock = new StockQuantity
                {
                    SiteId = siteId,
                    UsrId = userId,
                    ItemId = itemId,
                    QtyReceived = quantity,
                    QtyStock = quantity
                };
                _dbContext.StockQuantities.Add(stock);
            }

            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Updates the status of the Purchase Order based on the in-stock quantities (existing logic).
        /// </summary>
        private async Task UpdatePurchaseOrderStatusAsync(int poId)
        {
            var purchaseOrder = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Poid == poId);
            if (purchaseOrder != null)
            {
                var materialDetails = await _dbContext.MaterialDetails
                    .Where(m => m.MaterialId == purchaseOrder.MaterialId)
                    .ToListAsync();
                int itemCount = materialDetails.Count;
                int count = 0;

                foreach (var item in materialDetails)
                {
                    double getQty = await _dbContext.InStocks
                        .Where(s => s.POId == poId && s.ItemId == item.ItemId)
                        .SumAsync(q => (double?)q.Quantity) ?? 0.0;

                    if (item.Quantity <= getQty)
                        count++;
                }

                if (itemCount == count)
                {
                    var getMaterial = await _dbContext.MaterialRequests
                        .FirstOrDefaultAsync(ma => ma.MaterialId == purchaseOrder.MaterialId);
                    if (getMaterial != null)
                    {
                        if (getMaterial.Status == "Transfer In Delivery")
                            getMaterial.Status = "Transfer Completed from Warehouse";
                        else
                            getMaterial.Status = "Request Delivered";

                        _dbContext.MaterialRequests.Update(getMaterial);
                        await _dbContext.SaveChangesAsync();
                    }
                }
            }
        }

        // Fix method comparison error
        private bool IsMethodName(int value)
        {
            return value == 1; // Replace with your actual comparison logic
        }
    }
}

