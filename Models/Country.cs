using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class Country
{
    public int CountryId { get; set; }

    public string? CountryCode { get; set; }

    public string? CountryName { get; set; }

    public string? CountryIcon { get; set; }

    public virtual ICollection<UserCountry> UserCountries { get; set; } = new List<UserCountry>();
}
