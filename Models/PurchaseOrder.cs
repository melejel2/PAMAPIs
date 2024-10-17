using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class PurchaseOrder
{
    public int Poid { get; set; }

    public string? Ponumber { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Currency { get; set; }

    public string? Postatus { get; set; }

    public int? MaterialId { get; set; }

    public int? SupId { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public DateOnly Date { get; set; }

    public DateOnly ExpectedDate { get; set; }

    public bool? IsInternalTransfer { get; set; }

    public bool? IsReturned { get; set; }

    public double? Vat { get; set; }

    public bool? IsVatunbilled { get; set; }

    public bool? IsVatsuspended { get; set; }

    public DateOnly? CreatedDate { get; set; }

    public string? Comments { get; set; }

    public string? RejectionNote { get; set; }

    public bool? IsVatdiffere { get; set; }
}
