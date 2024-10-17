using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class CountryCurrency
{
    public int CurrencyId { get; set; }

    public int CountryId { get; set; }

    public string? Currency { get; set; }
}
