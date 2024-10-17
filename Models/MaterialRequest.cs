using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PAMAPIs.Models;

public partial class MaterialRequest
{
    public int MaterialId { get; set; }

    public int MaterialNumber { get; set; }

    public string? RefNo { get; set; }

    public string? RejectionNote { get; set; }

    public string? Status { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    public DateTime Date { get; set; }

    public bool? IsApprovedByPm { get; set; }

    public string? Remarks { get; set; }

    [ForeignKey("SiteId")]
    public virtual Site GetSites { get; set; }
}
