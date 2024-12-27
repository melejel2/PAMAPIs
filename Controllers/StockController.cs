// Controllers/StockController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PAMAPIs.Models;
using PAMAPIs.Data;
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

        public StockController(PAMContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Retrieves the stock status for a specific site.
        /// </summary>
        /// <param name="siteId">The ID of the site.</param>
        /// <returns>A list of StockVM representing the stock status.</returns>
        [HttpGet("GetSiteStockStatus/{siteId}")]
        public async Task<ActionResult<List<StockVM>>> GetSiteStockStatus(int siteId)
        {
            // Fetch the site information and ensure the site is not dead
            var site = await _dbContext.Sites
                .FirstOrDefaultAsync(s => s.SiteId == siteId && !s.IsDead);

            if (site == null)
            {
                // Return 404 Not Found if the site is not found or is dead
                return NotFound(new { Message = "Site not found or is inactive." });
            }

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

                                  // Calculate sums directly in the database
                                  Requested = _dbContext.MaterialDetails
                                      .Where(md => md.ItemId == item.ItemId && md.SiteId == siteId)
                                      .Sum(md => (double?)md.Quantity) ?? 0,

                                  Ordered = _dbContext.PoDetails
                                      .Where(po => po.ItemId == item.ItemId && po.SiteId == siteId)
                                      .Sum(po => (double?)po.Qty) ?? 0,

                                  Received = (_dbContext.InStocks
                                                  .Where(ins => ins.ItemId == item.ItemId && ins.SiteId == siteId)
                                                  .Sum(ins => (double?)ins.Quantity) ?? 0)
                                             + (_dbContext.InWarehouses
                                                  .Where(iw => iw.ItemId == item.ItemId && iw.SiteId == siteId)
                                                  .Sum(iw => (double?)iw.Quantity) ?? 0),

                                  Consumed = (_dbContext.OutStocks
                                                  .Where(outs => outs.ItemId == item.ItemId && outs.SiteId == siteId)
                                                  .Sum(outs => (double?)outs.Quantity) ?? 0)
                                             + (_dbContext.OutWarehouses
                                                  .Where(ow => ow.ItemId == item.ItemId && ow.SiteId == siteId)
                                                  .Sum(ow => (double?)ow.Quantity) ?? 0)
                              }).ToListAsync();

            // Optionally filter out items where all quantities are zero
            data = data.Where(d => d.Requested != 0 || d.Ordered != 0 || d.Received != 0 || d.Consumed != 0).ToList();

            return Ok(data);
        }

        /// <summary>
        /// Handles InStock operations.
        /// </summary>
        /// <param name="inStockDto">The InStock data.</param>
        /// <returns>Result of the operation.</returns>
        [HttpPost("InStock")]
        [Authorize] // Ensure the endpoint is secured
        public async Task<IActionResult> CreateInStock([FromBody] InStockDto inStockDto)
        {
            if (inStockDto == null)
            {
                return BadRequest(new { Message = "Invalid InStock data." });
            }

            // Validate the model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Retrieve user-related information from claims or other authentication mechanisms
                // For simplicity, we'll assume these values are passed in headers or claims
                // Replace these with your actual authentication retrieval logic

                // Example: Retrieving from headers (not recommended for production)
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

                // Validate PO existence
                var purchaseOrder = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Poid == inStockDto.POId);
                if (purchaseOrder == null)
                {
                    return BadRequest(new { Message = "No PO record found." });
                }

                // Validate Item existence
                var item = await _dbContext.Items.FirstOrDefaultAsync(i => i.ItemId == inStockDto.ItemId);
                if (item == null)
                {
                    return BadRequest(new { Message = "No Item record found." });
                }

                // Ensure SiteId is set
                if (inStockDto.SiteId == 0)
                {
                    inStockDto.SiteId = purchaseOrder.SiteId;
                    if (inStockDto.SiteId == 0)
                    {
                        return BadRequest(new { Message = "No Site record found for the PO. Please contact support." });
                    }
                }

                // Calculate PO Quantity with 10% buffer
                var poQty = await _dbContext.PoDetails
                                    .Where(p => p.Poid == inStockDto.POId && p.ItemId == inStockDto.ItemId)
                                    .SumAsync(s => (double?)s.Qty) ?? 0.0;
                poQty = Math.Round(poQty + poQty * 0.1, 3);

                // Calculate current stock quantity
                var stockQty = await _dbContext.InStocks
                                    .Where(p => p.POId == inStockDto.POId && p.ItemId == inStockDto.ItemId)
                                    .SumAsync(s => (double?)s.Quantity) ?? 0.0;
                var stockQtyInWare = await _dbContext.InWarehouses
                                        .Where(p => p.Poid == inStockDto.POId && p.ItemId == inStockDto.ItemId)
                                        .SumAsync(s => (double?)s.Quantity) ?? 0.0;
                stockQty += stockQtyInWare + inStockDto.Quantity;

                if (stockQty > poQty)
                {
                    return BadRequest(new { Message = $"You cannot do InStock for a purchase order by a quantity that is more than 10% of the initial ordered quantity. Max Ordered Qty = {poQty}" });
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
                {
                    inStockDto.Date = DateTime.Now;
                }

                // Check if Site is a Warehouse Site
                bool isWarehouseSite = await IsWarehouseSiteAsync(inStockDto.SiteId);
                if (isWarehouseSite)
                {
                    int supplierId = await GetSupplierIdAsync(inStockDto.POId);
                    await WarehouseInStockAsync(userId, inStockDto.SiteId, inStockDto.Quantity, inStockDto.ItemId, supplierId, inStockDto.POId, inStockDto.PODetailId, inStockDto.InNo, inStockDto.RefNo, inStockDto.SuppDeliveryNote, inStockDto.Date.Value);
                }
                else
                {
                    // Add InStock record
                    InStock inStock = new InStock
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
                }

                // Update Purchase Order Status if necessary
                await UpdatePurchaseOrderStatusAsync(inStockDto.POId);

                return Ok(new { Message = "InStock operation successful." });
            }
            catch (Exception ex)
            {
                // Log the exception (implement logging as needed)
                // _logger.LogError(ex, "Error occurred while creating InStock.");

                return StatusCode(500, new { Message = $"An error occurred: {ex.Message}" });
            }
        }

        /// <summary>
        /// Handles OutStock operations.
        /// </summary>
        /// <param name="outStockDto">The OutStock data.</param>
        /// <returns>Result of the operation.</returns>
        [HttpPost("OutStock")]
        [Authorize] // Ensure the endpoint is secured
        public async Task<IActionResult> CreateOutStock([FromBody] OutStockDto outStockDto)
        {
            if (outStockDto == null)
            {
                return BadRequest(new { Message = "Invalid OutStock data." });
            }

            // Validate the model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Retrieve user-related information from claims or other authentication mechanisms
                // For simplicity, we'll assume these values are passed in headers or claims
                // Replace these with your actual authentication retrieval logic

                // Example: Retrieving from headers (not recommended for production)
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

                // Assign SiteId if not provided
                if (outStockDto.SiteId == 0)
                {
                    outStockDto.SiteId = siteId;
                }

                // Handle 'search' parameter logic
                if (!string.IsNullOrEmpty(outStockDto.Search))
                {
                    if (outStockDto.Search == "2" || outStockDto.Search == "3")
                    {
                        if (outStockDto.ItemId == 0 || outStockDto.Quantity == 0)
                        {
                            return BadRequest(new { Message = "Please fill up the fields." });
                        }

                        // Set SubId based on 'search' parameter
                        outStockDto.SubId = outStockDto.Search == "2" ? 0 : 1;
                        outStockDto.NumId = 0;

                        // Create OutStock entity
                        OutStock outStock = new OutStock
                        {
                            ItemId = outStockDto.ItemId,
                            Quantity = outStockDto.Quantity,
                            RefNo = outStockDto.RefNo,
                            OutNo = outStockDto.OutNo,
                            Date = outStockDto.Date,
                            SiteId = outStockDto.SiteId,
                            UsrId = userId,
                            SubId = outStockDto.SubId.Value,
                            NumId = outStockDto.NumId.Value
                        };
                        _dbContext.OutStocks.Add(outStock);

                        // Add to WarehouseTemp if 'search' == "2"
                        if (outStockDto.Search == "2")
                        {
                            WarehouseTemp temp = new WarehouseTemp
                            {
                                Quantity = outStockDto.Quantity,
                                ItemId = outStockDto.ItemId,
                                SiteId = outStockDto.SiteId,
                                UsrId = userId
                            };
                            _dbContext.WarehouseTemps.Add(temp);
                        }

                        // Update StockQuantity
                        var checkItem = await _dbContext.StockQuantities
                                            .FirstOrDefaultAsync(i => i.ItemId == outStockDto.ItemId && i.SiteId == outStockDto.SiteId && i.UsrId == userId);
                        if (checkItem != null)
                        {
                            checkItem.QtyStock -= outStockDto.Quantity;
                            _dbContext.StockQuantities.Update(checkItem);
                        }
                        else
                        {
                            return BadRequest(new { Message = "StockQuantity record not found for the item." });
                        }

                        await _dbContext.SaveChangesAsync();
                        return Ok(new { Message = "OutStock operation successful." });
                    }
                    else
                    {
                        return BadRequest(new { Message = "Invalid search parameter." });
                    }
                }
                else
                {
                    // Handle case where 'search' is not provided
                    if (outStockDto.ItemId == 0 || outStockDto.SubId == 0 || outStockDto.NumId == 0)
                    {
                        return BadRequest(new { Message = "Please fill up the fields." });
                    }

                    // Create OutStock entity
                    OutStock outStock = new OutStock
                    {
                        ItemId = outStockDto.ItemId,
                        Quantity = outStockDto.Quantity,
                        RefNo = outStockDto.RefNo,
                        OutNo = outStockDto.OutNo,
                        Date = outStockDto.Date,
                        SiteId = outStockDto.SiteId,
                        UsrId = userId,
                        SubId = outStockDto.SubId.Value,
                        NumId = outStockDto.NumId.Value
                    };
                    _dbContext.OutStocks.Add(outStock);

                    // Update StockQuantity
                    var checkItem = await _dbContext.StockQuantities
                                        .FirstOrDefaultAsync(i => i.ItemId == outStockDto.ItemId && i.SiteId == outStockDto.SiteId && i.UsrId == userId);
                    if (checkItem != null)
                    {
                        checkItem.QtyStock -= outStockDto.Quantity;
                        _dbContext.StockQuantities.Update(checkItem);
                    }
                    else
                    {
                        return BadRequest(new { Message = "StockQuantity record not found for the item." });
                    }

                    await _dbContext.SaveChangesAsync();
                    return Ok(new { Message = "OutStock operation successful." });
                }
            }
            catch (Exception ex)
            {
                // Log the exception (implement logging as needed)
                // _logger.LogError(ex, "Error occurred while creating OutStock.");

                return StatusCode(500, new { Message = "Please contact soft support.", Details = ex.Message });
            }
        }

        #region Helper Methods

        /// <summary>
        /// Determines if the given site is a warehouse site.
        /// </summary>
        /// <param name="siteId">The site ID.</param>
        /// <returns>True if warehouse site; otherwise, false.</returns>
        private async Task<bool> IsWarehouseSiteAsync(int siteId)
        {
            // Implement logic to determine if the site is a warehouse site
            // For example, check a property like SiteType or a related entity
            var site = await _dbContext.Sites.FirstOrDefaultAsync(s => s.SiteId == siteId);
            return site != null/* && site.IsWarehouse*/; // Assuming there is an IsWarehouse property needs addressing
        }

        /// <summary>
        /// Retrieves the Supplier ID based on the Purchase Order ID.
        /// </summary>
        /// <param name="poId">The Purchase Order ID.</param>
        /// <returns>The Supplier ID.</returns>
        private async Task<int> GetSupplierIdAsync(int poId)
        {
            var purchaseOrder = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Poid == poId);
            return purchaseOrder?.SupId ?? 0;
        }

        /// <summary>
        /// Handles warehouse in-stock operations.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="siteId">Site ID.</param>
        /// <param name="quantity">Quantity.</param>
        /// <param name="itemId">Item ID.</param>
        /// <param name="supplierId">Supplier ID.</param>
        /// <param name="poId">Purchase Order ID.</param>
        /// <param name="podetailId">Purchase Order Detail ID.</param>
        /// <param name="inNo">InStock Number.</param>
        /// <param name="refNo">Reference Number.</param>
        /// <param name="suppDeliveryNote">Supplier Delivery Note.</param>
        /// <param name="date">Date.</param>
        /// <returns></returns>
        private async Task WarehouseInStockAsync(int userId, int siteId, double quantity, int itemId, int supplierId, int poId, int podetailId, int inNo, string refNo, string suppDeliveryNote, DateTime date)
        {
            // Implement the business logic for warehouse in-stock operations
            // Example:
            // - Add to InStocks
            // - Update StockQuantities
            // - Any other necessary operations

            InStock inStock = new InStock
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

            // Update StockQuantity
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
        /// Updates the status of the Purchase Order based on the in-stock quantities.
        /// </summary>
        /// <param name="poId">The Purchase Order ID.</param>
        /// <returns></returns>
        private async Task UpdatePurchaseOrderStatusAsync(int poId)
        {
            var purchaseOrder = await _dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Poid == poId);
            if (purchaseOrder != null)
            {
                var materialDetails = await _dbContext.MaterialDetails.Where(m => m.MaterialId == purchaseOrder.MaterialId).ToListAsync();
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
                    var getMaterial = await _dbContext.MaterialRequests.FirstOrDefaultAsync(ma => ma.MaterialId == purchaseOrder.MaterialId);
                    if (getMaterial != null)
                    {
                        if (getMaterial.Status == "Transfer In Delivery")
                        {
                            getMaterial.Status = "Transfer Completed from Warehouse";
                        }
                        else
                        {
                            getMaterial.Status = "Request Delivered";
                        }
                        _dbContext.MaterialRequests.Update(getMaterial);
                        await _dbContext.SaveChangesAsync();
                    }
                }
            }
        }

        #endregion
    }
}
