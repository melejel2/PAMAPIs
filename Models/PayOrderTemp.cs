using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class PayOrderTemp
{
    public int PayOrderTempId { get; set; }

    public int? Poid { get; set; }

    public int? PodetailId { get; set; }

    public int? DeliveryNoteId { get; set; }

    public int? ItemId { get; set; }

    public double? PreQty { get; set; }

    public double? ActualQty { get; set; }

    public double? CumulQty { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public bool? IsWarehouseSite { get; set; }
}
