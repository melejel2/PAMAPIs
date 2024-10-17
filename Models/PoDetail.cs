using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PAMAPIs.Models;

public partial class PoDetail
{
    public int PodetailId { get; set; }

    public int? Poid { get; set; }

    public double? Qty { get; set; }

    public double? UnitPrice { get; set; }

    public int? ItemId { get; set; }

    public int? CodeId { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public bool? IsInternalTransfer { get; set; }

    public bool? IsVatbilled { get; set; }

    [ForeignKey("CodeId")]
    public virtual CostCode GetCost_Codes { get; set; }
}
