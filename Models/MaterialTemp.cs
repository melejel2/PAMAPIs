using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PAMAPIs.Models;

public partial class MaterialTemp
{
    public int MaterialTempId { get; set; }

    public double? Quantity { get; set; }

    public int? SubId { get; set; }

    public int? CategoryId { get; set; }

    public int? ItemId { get; set; }

    public int? CodeId { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    [ForeignKey("CodeId")]
    public virtual CostCode GetCost_Codes { get; set; }

}
