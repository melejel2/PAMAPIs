using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class OutStock
{
    public int OutId { get; set; }

    public int? OutNo { get; set; }

    public string? RefNo { get; set; }

    public double? Quantity { get; set; }

    public DateTime Date { get; set; }

    public int? ItemId { get; set; }

    public int SiteId { get; set; }

    public int? SubId { get; set; }

    public int? NumId { get; set; }

    public int? UsrId { get; set; }
}
