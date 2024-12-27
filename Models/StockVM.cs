// Example: PAMAPIs/Models/StockVM.cs
namespace PAMAPIs.Models
{
    public class StockVM
    {
        public int ItemId { get; set; }
        public string Item { get; set; }
        public string Unit { get; set; }
        public string CategoryName { get; set; }
        public string SiteName { get; set; }
        public string Acronym { get; set; }
        public double Requested { get; set; }
        public double Ordered { get; set; }
        public double Received { get; set; }
        public double Consumed { get; set; }
    }
}
