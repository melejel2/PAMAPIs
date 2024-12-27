using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PAMAPIs.Models;
using PAMAPIs.Data;
using Microsoft.EntityFrameworkCore;

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
    }
}