using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class Supplier
{
    public int SupId { get; set; }

    public string? SupName { get; set; }

    public string? SupRepresentative { get; set; }

    public string? SupContactNo { get; set; }

    public string? SupEmail { get; set; }

    public string? SupFax { get; set; }

    public string? SupAddress { get; set; }

    public string? PaymentTerms { get; set; }

    public int CountryId { get; set; }
}
