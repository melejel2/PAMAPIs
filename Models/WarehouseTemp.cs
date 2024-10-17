using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class WarehouseTemp
{
    public int WarehouseTempId { get; set; }

    public double? Quantity { get; set; }

    public int? ItemId { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public bool? IsApproved { get; set; }
}
