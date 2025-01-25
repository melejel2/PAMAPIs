// OutStockRequest.cs
using System;

namespace PAMAPIs.Models
{
    public class OutStockRequest
    {
        // Additional fields to avoid CS1061 errors
        public int RoleId { get; set; }  // e.g. 4, 5, 7, 10
        public int UserId { get; set; }  // e.g. 93
        public int SiteId { get; set; }  // e.g. 46

        public int ItemId { get; set; }
        public double Quantity { get; set; }
        public string? RefNo { get; set; }       // made nullable
        public int? OutNo { get; set; }
        public DateTime? Date { get; set; }
        public string? Search { get; set; }      // "1" => Subcontractor, "3" => Site Cons., "4" => Other Site
        public int? ToSiteId { get; set; }
        public string? Remarks { get; set; }
        public string? OutStockNote { get; set; }
        public int? SubId { get; set; }
        public int? NumId { get; set; }
    }
}