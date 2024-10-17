using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class Item
{
    public int ItemId { get; set; }

    public string? ItemName { get; set; }

    public string? ItemUnit { get; set; }

    public int? CategoryId { get; set; }
}
