using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class Vat
{
    public int VatId { get; set; }

    public int CountryId { get; set; }

    public double? Vat1 { get; set; }
}
