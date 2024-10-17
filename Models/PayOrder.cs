using System;
using System.Collections.Generic;

namespace PAMAPIs.Models;

public partial class PayOrder
{
    public int PayOrderId { get; set; }

    public int? Poid { get; set; }

    public string? PayOrderNumber { get; set; }

    public string? SupplierInvoiceNum { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public DateOnly? Date { get; set; }
}
