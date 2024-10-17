using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class SupplierItem
{
    public int SupItemId { get; set; }

    public int? ItemId { get; set; }

    public int? SupId { get; set; }

    public double? UnitPrice { get; set; }

    public DateOnly? Date { get; set; }
}
