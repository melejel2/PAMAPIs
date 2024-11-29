using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PAMAPIs.Models;

public partial class Site
{
    [Key]
    public int SiteId { get; set; }

    public string SiteCode { get; set; }

    public string SiteName { get; set; }

    public string CityName { get; set; }

    public string Acronym { get; set; }

    public int CountryId { get; set; }

    [ForeignKey("CountryId")]
    public virtual Country GetCountry { get; set; }

    public bool IsDead { get; set; }

    // Add the new Type property without default value
    [Required]
    [StringLength(50)]
    public string Type { get; set; }
}
