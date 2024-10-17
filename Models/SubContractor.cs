using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class SubContractor
{
    public int SubId { get; set; }

    public string? SubName { get; set; }

    public int CountryId { get; set; }

    public int? UsrId { get; set; }
}
