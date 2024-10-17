using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PAMAPIs.Models;

public partial class OutWarehouse
{
    public int OutWareId { get; set; }

    public int? OutWareNo { get; set; }

    public string? RefNo { get; set; }

    public double? Quantity { get; set; }

    public int? ItemId { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public DateOnly? Date { get; set; }

    [ForeignKey("ItemId")]
    public virtual Item GetItems { get; set; }

    [ForeignKey("SiteId")]
    public virtual Site GetSites { get; set; }

    [ForeignKey("UsrId")]
    public virtual User Users { get; set; }
}
