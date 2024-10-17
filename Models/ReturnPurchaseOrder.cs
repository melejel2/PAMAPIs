using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class ReturnPurchaseOrder
{
    public int Rpoid { get; set; }

    public string? ReturnNo { get; set; }

    public int? Poid { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public bool? IsReturnedAmount { get; set; }

    public double? Amount { get; set; }

    public DateOnly? CreatedDate { get; set; }
}
