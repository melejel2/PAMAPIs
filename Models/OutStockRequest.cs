using System;

namespace PAMAPIs.Models
{
    public class OutStockRequest
    {
        public int ItemId { get; set; }
        public double Quantity { get; set; }
        public string RefNo { get; set; }
        public int? OutNo { get; set; }
        public DateTime? Date { get; set; }
        public string Search { get; set; }        // "1" => Subcontractor, "3" => Site Cons., "4" => Other Site
        public int? ToSiteId { get; set; }
        public string Remarks { get; set; }
        public string OutStockNote { get; set; }  
        public int? SubId { get; set; }
        public int? NumId { get; set; }
    }
}
