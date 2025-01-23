using PAMAPIs.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PAM.Models.EntityModels
{
    [Table("OutStock")]
    public class OutStock
    {
        [Key]
        public int OutId { get; set; }
        public int? OutNo { get; set; }
        public string RefNo { get; set; }
        public double? Quantity { get; set; }
        public DateTime? Date { get; set; }
        public int? ItemId { get; set; }
        public int? SiteId { get; set; }
        public int? SubId { get; set; }
        public int? NumId { get; set; }
        public int? UsrId { get; set; }
        public int? ToSiteId { get; set; }
        public bool? IsApprovedByOP { get; set; }
        public string Remarks { get; set; }
        public string OutStockNote { get; set; }

        [ForeignKey("ItemId")]
        public virtual Item GetItems { get; set; }

        [ForeignKey("SiteId")]
        public virtual Site GetSites { get; set; }

        [ForeignKey("SubId")]
        public virtual SubContractor SubContractors { get; set; }

        [ForeignKey("NumId")]
        public virtual SubContractNumber SubContractNumber { get; set; }

        [ForeignKey("ToSiteId")]
        public virtual Site ToSite { get; set; }

        [ForeignKey("UsrId")]
        public virtual User GetUsers { get; set; }
    }
}
