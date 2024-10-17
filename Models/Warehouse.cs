using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class Warehouse
{
    public int WarehouseId { get; set; }

    public int? ItemId { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public DateOnly? Date { get; set; }
}
