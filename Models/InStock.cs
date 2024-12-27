using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class InStock
{
    public int InId { get; set; }

    public int? InNo { get; set; }

    public string? RefNo { get; set; }

    public double? Quantity { get; set; }

    public DateTime? Date { get; set; }

    public int? ItemId { get; set; }

    public int SiteId { get; set; }

    public int? Poid { get; set; }

    public int? PodetailId { get; set; }

    public int? UsrId { get; set; }

    public string? SuppDeliveryNote { get; set; }

    public int POId { get; set; }

    public int? PODetailId { get; set; }

}
