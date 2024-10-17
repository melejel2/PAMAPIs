using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class WarehouseQuantity
{
    public int Wqty { get; set; }

    public double? QtyReceived { get; set; }

    public double? QtyStock { get; set; }

    public int? ItemId { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }
}
