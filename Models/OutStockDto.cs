// Models/OutStockDto.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace PAMAPIs.Models
{
    public class OutStockDto
    {
        [Required]
        public int ItemId { get; set; }

        [Required]
        [Range(0.1, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        public double Quantity { get; set; }

        [Required]
        public string RefNo { get; set; }

        [Required]
        public int OutNo { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public string Search { get; set; } // Represents the 'search' parameter
        public int? SubId { get; set; }
        public int? NumId { get; set; }

        public int SiteId { get; set; }
    }
}
