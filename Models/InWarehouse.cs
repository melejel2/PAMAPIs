using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace PAMAPIs.Models;

public partial class InWarehouse
{
    public int InWareId { get; set; }

    public int? WareNo { get; set; }

    public string? RefNo { get; set; }

    public double? Quantity { get; set; }

    public int? ItemId { get; set; }

    public int SiteId { get; set; }

    public int? SupId { get; set; }

    public int? UsrId { get; set; }

    public DateTime Date { get; set; }

    public int? Poid { get; set; }

    public int? PodetailId { get; set; }

    public string? SuppDeliveryNote { get; set; }


    //public TransferType TransferType { get; set; }
    
    [ForeignKey("ItemId")]
    public virtual Item GetItems { get; set; }
    
    [ForeignKey("SiteId")]
    public virtual Site GetSites { get; set; }
    
    [ForeignKey("UsrId")]
    public virtual User GetUsers { get; set; }
    
    [ForeignKey("POId")]
    public virtual PurchaseOrder GetPurchase_Order { get; set; }
    
    [ForeignKey("SupId")]
    public virtual Supplier GetSuppliers { get; set; }
    
    [ForeignKey("PODetailId")]
    public virtual PoDetail GetPO_Detail { get; set; }

}
