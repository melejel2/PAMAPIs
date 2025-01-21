using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class PayOrderDetail
{
    public int PayOrderDetailId { get; set; }

    public int? PayOrderId { get; set; }

    public int? PodetailId { get; set; }

    public int? DeliveryNoteId { get; set; }

    public int? ItemId { get; set; }

    public double? PreQty { get; set; }

    public double? ActualQty { get; set; }

    public double? CumulQty { get; set; }

    public int SiteId { get; set; }
}
