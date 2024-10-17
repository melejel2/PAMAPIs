using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PAMAPIs.Models;

public partial class MaterialDetail
{
    public int MaterialDetailId { get; set; }

    public int? MaterialId { get; set; }

    public double Quantity { get; set; }

    public int? SubId { get; set; }

    public int? CategoryId { get; set; }

    public int? ItemId { get; set; }

    public int? CodeId { get; set; }

    public int SiteId { get; set; }

    public int? UsrId { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category GetCategory { get; set; }

    [ForeignKey("ItemId")]
    public virtual Item GetItems { get; set; }

    [ForeignKey("CodeId")]
    public virtual CostCode GetCost_Codes { get; set; }

    [ForeignKey("SiteId")]
    public virtual Site GetSites { get; set; }

    [ForeignKey("MaterialId")]
    public virtual MaterialRequest GetMaterial_Request { get; set; }

    [ForeignKey("SubId")]
    public virtual SubContractor GetSubContractors { get; set; }

    [ForeignKey("UsrId")]
    public virtual User GetUsers { get; set; }

}
