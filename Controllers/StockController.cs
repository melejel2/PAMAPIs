using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PAM.Models.EntityModels;
using PAMAPIs.Data;
using PAMAPIs.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading.Tasks;

namespace PAMAPIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly PAMContext _dbContext;

        public StockController(PAMContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Retrieves the "stock status" for a specific site.
        /// GET: api/Stock/GetSiteStockStatus/{siteId}
        /// </summary>
        [HttpGet("GetSiteStockStatus/{siteId}")]
        public async Task<ActionResult<List<StockVM>>> GetSiteStockStatus(int siteId)
        {
            var site = await _dbContext.Sites
                .FirstOrDefaultAsync(s => s.SiteId == siteId && !s.IsDead);

            if (site == null)
                return NotFound(new { Message = "Site not found or is inactive." });

            var data = await (
                from item in _dbContext.Items
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

                    Received = _dbContext.InStocks
                        .Where(ins => ins.ItemId == item.ItemId && ins.SiteId == siteId)
                        .Sum(ins => (double?)ins.Quantity) ?? 0,

                    Consumed = _dbContext.OutStocks
                        .Where(outs => outs.ItemId == item.ItemId && outs.SiteId == siteId)
                        .Sum(outs => (double?)outs.Quantity) ?? 0
                }
            ).ToListAsync();

            // Optionally filter out items with all zero
            data = data.Where(d => d.Requested != 0 
                                || d.Ordered != 0 
                                || d.Received != 0 
                                || d.Consumed != 0).ToList();

            return Ok(data);
        }

        // ------------------------------------------------------------------------
        // 1) GET UNIT => "GetUnit?itemId=123&siteId=45"
        // ------------------------------------------------------------------------
        [HttpGet("GetUnit")]
        public async Task<IActionResult> GetUnit([FromQuery] int itemId, [FromQuery] int siteId)
        {
            try
            {
                if (itemId <= 0 || siteId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid itemId or siteId." });
                }

                var item = await _dbContext.Items.FirstOrDefaultAsync(a => a.ItemId == itemId);
                if (item == null)
                {
                    return NotFound(new { success = false, message = $"Item ID={itemId} not found in DB." });
                }

                double availableQty = await ComputeAvailableOutStockQty(itemId, siteId);

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
        // 2) GET STOCK OUT STATUS => "GetStockOutStatus/{siteId}"
        //    Returns denormalized data including itemName, subName, etc.
        // ------------------------------------------------------------------------
        [HttpGet("GetStockOutStatus/{siteId}")]
        public async Task<IActionResult> GetStockOutStatus(int siteId)
        {
            try
            {
                if (siteId <= 0)
                    return BadRequest(new { message = "Invalid siteId." });

                var outList = await _dbContext.OutStocks
                    .Where(o => o.SiteId == siteId)
                    .OrderByDescending(o => o.OutId)
                    .Select(o => new
                    {
                        // Basic OutStock fields
                        o.OutId,
                        o.OutNo,
                        o.RefNo,
                        o.Quantity,
                        o.Date,
                        o.OutStockNote,

                        // Navigation => "GetItems" instead of "Item"
                        ItemName = o.GetItems.ItemName,
                        ItemUnit = o.GetItems.ItemUnit,

                        // "Entity" => subName or siteName or consumption
                        SubName = (o.ToSiteId != null && o.ToSiteId != 0)
                            ? o.ToSite.SiteName
                            : (o.SubId == 1
                                ? "Site Consumption"
                                : (o.SubId > 1
                                    ? o.SubContractors.SubName
                                    : "Site Consumption")),

                        // ContractNumber only if o.NumId != 0
                        ContractNumber = (o.NumId != null && o.NumId != 0)
                            ? o.SubContractNumber.ContractNumber
                            : ""
                    })
                    .ToListAsync();

                // Convert date to string or keep as is
                var finalList = outList.Select(x => new
                {
                    outId = x.OutId,
                    outNo = x.OutNo,
                    refNo = x.RefNo,
                    quantity = x.Quantity,
                    // For date, you can do x.Date?.ToString("yyyy-MM-dd") or pass the raw
                    date = x.Date,
                    outStockNote = x.OutStockNote,

                    itemName = x.ItemName,
                    itemUnit = x.ItemUnit,
                    subName = x.SubName,
                    contractNumber = x.ContractNumber
                });

                return Ok(finalList);
            }
            catch (SqlNullValueException ex)
            {
                return StatusCode(500, new
                {
                    message = ex.Message,
                    debug = "Check for null fields in OutStocks table."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ------------------------------------------------------------------------
        // 3) POPULATE SITES FOR "OTHER SITE"
        // ------------------------------------------------------------------------
        [HttpGet("PopulateSitesForOtherSite")]
        public async Task<IActionResult> PopulateSitesForOtherSite([FromQuery] int siteId)
        {
            try
            {
                if (siteId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid siteId." });
                }

                var currentSite = await _dbContext.Sites.FindAsync(siteId);
                if (currentSite == null)
                {
                    return NotFound(new { success = false, message = "Current site not found." });
                }

                int countryId = currentSite.CountryId;

                var list = await _dbContext.Sites
                    .Where(s => s.CountryId == countryId && s.SiteId != siteId)
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
        // 4) POPULATE CONTRACT NUMBERS => e.g. GET: api/Stock/PopulateNum?subId=11&siteId=45
        // ------------------------------------------------------------------------
        [HttpGet("PopulateNum")]
        public async Task<IActionResult> PopulateNum([FromQuery] int subId, [FromQuery] int siteId)
        {
            try
            {
                if (subId <= 0 || siteId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid subId or siteId." });
                }

                var data = await _dbContext.SubContractNumbers
                    .Where(sc => sc.SubId == subId && sc.SiteId == siteId)
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
        // 5) IN-STOCK OPERATION => POST: api/Stock/InStock
        // ------------------------------------------------------------------------
        [HttpPost("InStock")]
        [Authorize]
        public async Task<IActionResult> CreateInStock([FromBody] InStockDto inStockDto)
        {
            if (inStockDto == null)
                return BadRequest(new { Message = "Invalid InStock data (null)." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                int userId = inStockDto.SiteId; 
                int siteId = inStockDto.SiteId;

                // Actually, you likely want:
                userId = 0; // or read from a separate property if your DTO includes it
                userId = 999; // or set from JWT claims, etc. 
                // We'll assume "UserId" is also part of InStockDto if you want.

                userId = 0; // override or ensure it's valid
                // If you have "inStockDto.UserId" in the model, use that:
                // userId = inStockDto.UserId; // if you do store user ID in the body

                if (siteId <= 0)
                    return BadRequest(new { Message = "Invalid siteId in request body." });

                // Validate PO
                var purchaseOrder = await _dbContext.PurchaseOrders
                    .FirstOrDefaultAsync(p => p.Poid == inStockDto.POId);
                if (purchaseOrder == null)
                    return BadRequest(new { Message = "No PO record found." });

                // Validate item
                var item = await _dbContext.Items.FirstOrDefaultAsync(i => i.ItemId == inStockDto.ItemId);
                if (item == null)
                    return BadRequest(new { Message = "No Item record found." });

                // If site not set, fallback
                if (siteId == 0)
                {
                    siteId = purchaseOrder.SiteId;
                    if (siteId == 0)
                        return BadRequest(new { Message = "No Site record found for the PO." });
                }

                // PO quantity (+10% buffer)
                var poQty = await _dbContext.PoDetails
                    .Where(p => p.Poid == inStockDto.POId && p.ItemId == inStockDto.ItemId)
                    .SumAsync(s => (double?)s.Qty) ?? 0.0;
                poQty = Math.Round(poQty + poQty * 0.1, 3);

                // Current stock
                var stockQty = await _dbContext.InStocks
                    .Where(p => p.POId == inStockDto.POId && p.ItemId == inStockDto.ItemId)
                    .SumAsync(s => (double?)s.Quantity) ?? 0.0;

                double newTotal = stockQty + inStockDto.Quantity;
                if (newTotal > poQty)
                {
                    return BadRequest(new
                    {
                        Message = $"Cannot do InStock beyond 10% buffer. Max Qty = {poQty}"
                    });
                }

                // If PODetailId is zero => find one
                int podetailId = inStockDto.PODetailId;
                if (podetailId == 0)
                {
                    podetailId = await _dbContext.PoDetails
                        .Where(i => i.Poid == inStockDto.POId && i.ItemId == inStockDto.ItemId)
                        .Select(s => (int?)s.PodetailId)
                        .FirstOrDefaultAsync() ?? 0;
                }

                // If no date => default to now
                var dateVal = inStockDto.Date;
                if (!dateVal.HasValue || dateVal.Value.Year < 1900)
                {
                    dateVal = DateTime.Now;
                }

                // Insert
                var inStock = new InStock
                {
                    POId = inStockDto.POId,
                    ItemId = inStockDto.ItemId,
                    Quantity = inStockDto.Quantity,
                    PODetailId = podetailId,
                    UsrId = userId,
                    SiteId = siteId,
                    RefNo = inStockDto.RefNo ?? string.Empty,
                    InNo = inStockDto.InNo,
                    Date = dateVal.Value
                };
                _dbContext.InStocks.Add(inStock);

                // Update StockQuantity
                var checkItem = await _dbContext.StockQuantities
                    .FirstOrDefaultAsync(i => i.ItemId == inStockDto.ItemId && i.SiteId == siteId && i.UsrId == userId);

                if (checkItem != null)
                {
                    checkItem.QtyReceived += inStockDto.Quantity;
                    checkItem.QtyStock += inStockDto.Quantity;
                    _dbContext.StockQuantities.Update(checkItem);
                }
                else
                {
                    var stock = new StockQuantity
                    {
                        SiteId = siteId,
                        UsrId = userId,
                        ItemId = inStockDto.ItemId,
                        QtyReceived = inStockDto.Quantity,
                        QtyStock = inStockDto.Quantity
                    };
                    _dbContext.StockQuantities.Add(stock);
                }

                await _dbContext.SaveChangesAsync();

                // Update PO status
                await UpdatePurchaseOrderStatusAsync(inStockDto.POId);

                return Ok(new { Message = "InStock operation successful." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"An error occurred: {ex.Message}" });
            }
        }

        // ------------------------------------------------------------------------
        // 6) OUT-STOCK OPERATION => POST: api/Stock/OutStock
        // ------------------------------------------------------------------------
        [HttpPost("OutStock")]
        [Authorize]
        public async Task<IActionResult> CreateOutStock([FromBody] OutStockRequest dto)
        {
            try
            {
                var allowedRolesForStock = new HashSet<int> { 4, 5, 7, 10 };
                if (!allowedRolesForStock.Contains(dto.RoleId))
                {
                    return Forbid("You are not allowed to do OutStock.");
                }

                int userId = dto.UserId;
                int siteId = dto.SiteId;
                if (dto.ItemId == 0)
                    return BadRequest(new { success = false, message = "No item selected." });
                if (dto.Quantity <= 0)
                    return BadRequest(new { success = false, message = "Quantity must be > 0." });
                if (string.IsNullOrWhiteSpace(dto.OutStockNote))
                    return BadRequest(new { success = false, message = "OutStock note is required." });
                if (siteId <= 0 || userId <= 0)
                    return BadRequest(new { success = false, message = "Invalid siteId or userId." });

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

                switch (dto.Search)
                {
                    case "1": // Subcontractor
                        outStock.SubId = dto.SubId;
                        outStock.NumId = dto.NumId;
                        outStock.IsApprovedByOP = true;
                        break;
                    case "3": // Site Consumption
                        outStock.SubId = 1;
                        outStock.NumId = 0;
                        outStock.IsApprovedByOP = true;
                        break;
                    case "4": // Other Site
                        outStock.SubId = 0;
                        outStock.NumId = 0;
                        outStock.ToSiteId = dto.ToSiteId;
                        outStock.IsApprovedByOP = false;
                        break;
                    default:
                        return BadRequest(new { success = false, message = "Invalid 'Out Stock To' option." });
                }

                _dbContext.OutStocks.Add(outStock);

                var checkItem = await _dbContext.StockQuantities
                    .FirstOrDefaultAsync(i => i.ItemId == outStock.ItemId && i.SiteId == siteId);
                if (checkItem == null)
                {
                    return BadRequest(new { success = false, message = "No StockQuantity record found for that item/site." });
                }

                checkItem.QtyStock -= dto.Quantity;
                _dbContext.Entry(checkItem).State = EntityState.Modified;
                await _dbContext.SaveChangesAsync();

                // Build JSON for new row
                var item = await _dbContext.Items.FindAsync(outStock.ItemId);
                string itemName = item?.ItemName ?? "";
                string itemUnit = item?.ItemUnit ?? "";

                string subName;
                if (outStock.ToSiteId.HasValue && outStock.ToSiteId.Value != 0)
                {
                    subName = await _dbContext.Sites
                        .Where(s => s.SiteId == outStock.ToSiteId.Value)
                        .Select(s => s.SiteName)
                        .FirstOrDefaultAsync() ?? "Other Site";
                }
                else if (outStock.SubId == 1)
                {
                    subName = "Site Consumption";
                }
                else if (outStock.SubId > 1)
                {
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
                    Quantity = outStock.Quantity?.ToString("0.##"),
                    SubName = subName,
                    ContractNumber = contractNumber,
                    DateString = outStock.Date?.ToString("dd-MM-yyyy"),
                    OutId = outStock.OutId
                };

                // Notification if "Other Site"
                if (dto.Search == "4" && dto.ToSiteId.HasValue && dto.ToSiteId.Value != 0)
                {
                    await SendOutStockNotificationAsync(outStock);
                }

                return Ok(new
                {
                    success = true,
                    message = "OutStock saved successfully.",
                    newRow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error in OutStock: " + ex.Message });
            }
        }

        private async Task SendOutStockNotificationAsync(OutStock outStock)
        {
            try
            {
                var fromSite = await _dbContext.Sites.FindAsync(outStock.SiteId);
                var toSite = await _dbContext.Sites.FindAsync(outStock.ToSiteId);
                var item = await _dbContext.Items.FindAsync(outStock.ItemId);
                var user = await _dbContext.Users.FindAsync(outStock.UsrId);

                if (fromSite == null || toSite == null || item == null || user == null)
                    return;

                var pms = await _dbContext.Users
                    .Where(u => u.SiteId == toSite.SiteId && (u.RoleId == 7 || u.RoleId == 10))
                    .ToListAsync();
                if (!pms.Any())
                {
                    pms = await _dbContext.Users
                        .Where(u => u.SiteId == toSite.SiteId && u.RoleId == 4)
                        .ToListAsync();
                }
                if (!pms.Any()) return;

                var toEmails = pms
                    .Where(pm => !string.IsNullOrEmpty(pm.UserEmail))
                    .Select(pm => pm.UserEmail + "@seg-int.com")
                    .Distinct()
                    .ToList();

                var countryId = fromSite.CountryId;
                var ccEmails = new List<string>();
                switch (countryId)
                {
                    case 1: ccEmails.Add("ejabbour@seg-int.com"); break;
                    case 2: ccEmails.Add("belaridi@seg-int.com"); break;
                    case 3: ccEmails.Add("gkonaizeh@seg-int.com"); break;
                    default:
                        break;
                }

                string subject = $"Material Transfer: {fromSite.SiteName} -> {toSite.SiteName}";
                string greeting = (pms.Count == 1)
                    ? $"Dear <b>{pms.First().UserName}</b>"
                    : "Dear Project Managers";

                // Just an example email body
                string messageBody = $@"
<html>
<head><title>OutStock Notification</title></head>
<body>
    <h2 style='color: #007BFF;'>Material Transfer Notification</h2>
    <p>{greeting},</p>
    <p>This is to inform you that <b>{user.UserName}</b> from <b>{fromSite.SiteName}</b>
       has initiated a transfer of <b>{item.ItemName}</b> to <b>{toSite.SiteName}</b>.</p>
    <p><b>Quantity:</b> {outStock.Quantity} {item.ItemUnit}</p>
    <p><b>Reference No:</b> {outStock.RefNo}</p>
    <p><b>Date:</b> {outStock.Date:dd-MM-yyyy}</p>
    {(string.IsNullOrWhiteSpace(outStock.Remarks) ? "" : $"<p><b>Remarks:</b> {outStock.Remarks}</p>")}
</body>
</html>";

                // If you inject an emailService, call it:
                // await _emailService.SendEmailAsync(toEmails.ToArray(), subject, messageBody, ccEmails.ToArray());
            }
            catch
            {
                // swallow or log
            }
        }

        // HELPER: Compute available stock
        private async Task<double> ComputeAvailableOutStockQty(int itemId, int siteId)
        {
            var stockRec = await _dbContext.StockQuantities
                .FirstOrDefaultAsync(sq => sq.ItemId == itemId && sq.SiteId == siteId);

            if (stockRec == null) return 0.0;

            double usedQty = 0.0; 
            double available = (stockRec.QtyStock ?? 0.0) - usedQty;
            return available < 0 ? 0 : available;
        }

        // HELPER: Update PO status
        private async Task UpdatePurchaseOrderStatusAsync(int poId)
        {
            var purchaseOrder = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Poid == poId);
            if (purchaseOrder == null) return;

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
}