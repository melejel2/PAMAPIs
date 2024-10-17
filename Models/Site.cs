using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class Site
{
    public int SiteId { get; set; }

    public string? SiteCode { get; set; }

    public string? SiteName { get; set; }

    public string? CityName { get; set; }

    public string? Acronym { get; set; }

    public int CountryId { get; set; }
}
