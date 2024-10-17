using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class ReturnPurchaseOrderDetail
{
    public int RpodetailId { get; set; }

    public int? Rpoid { get; set; }

    public int? PodetailId { get; set; }

    public int? ItemId { get; set; }

    public double? Qty { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }
}
