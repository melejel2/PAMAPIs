using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class User
{
    public int UsrId { get; set; }

    public string? UserCode { get; set; }

    public string? UserName { get; set; }

    public string? UserEmail { get; set; }

    public string? UserPassword { get; set; }

    public int RoleId { get; set; }

    public int CountryId { get; set; }

    public int SiteId { get; set; }

    public bool UpdatePass { get; set; }

    public DateTime? LastLogin { get; set; }

    public virtual ICollection<UserCountry> UserCountries { get; set; } = new List<UserCountry>();
}
