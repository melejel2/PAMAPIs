using PAMAPIs.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PAM.Models.EntityModels
{
    [Table("InStock")]
    public class InStock
    {
        [Key]
        public int InId { get; set; }
        public int InNo { get; set; }
        public string RefNo { get; set; }
        public double Quantity { get; set; }
        public DateTime Date { get; set; }
        public int ItemId { get; set; }
        public int SiteId { get; set; }
        public int POId { get; set; }
        public int PODetailId { get; set; }
        public int UsrId { get; set; }
        public string SuppDeliveryNote { get; set; }
        public int? FromSiteId { get; set; }
        public string Remarks { get; set; }
        public int? OutId { get; set; }

        // Navigation properties
        [ForeignKey("SiteId")]
        public virtual Site GetSites { get; set; }

        [ForeignKey("ItemId")]
        public virtual Item GetItems { get; set; }

        [ForeignKey("POId")]
        public virtual PurchaseOrder GetPurchase_Order { get; set; }

        [ForeignKey("PODetailId")]
        public virtual PoDetail GetPO_Detail { get; set; }

        [ForeignKey("UsrId")]
        public virtual User GetUsers { get; set; }

        [ForeignKey("FromSiteId")]
        public virtual Site FromSite { get; set; }
    }
}
