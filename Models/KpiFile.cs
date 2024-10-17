using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class KpiFile
{
    public int Id { get; set; }

    public int SiteId { get; set; }

    public string? Kpifile1 { get; set; }
}
