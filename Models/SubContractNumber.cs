using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class SubContractNumber
{
    public int NumId { get; set; }

    public int? SubId { get; set; }

    public int SiteId { get; set; }

    public string? ContractNumber { get; set; }

    public string? Trade { get; set; }
}
