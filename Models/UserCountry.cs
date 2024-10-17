using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class UserCountry
{
    public int Id { get; set; }

    public int UsrId { get; set; }

    public int CountryId { get; set; }

    public virtual Country Country { get; set; } = null!;

    public virtual User Usr { get; set; } = null!;
}
