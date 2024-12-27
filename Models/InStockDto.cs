// Models/InStockDto.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace PAMAPIs.Models
{
    public class InStockDto
    {
        [Required]
        public int POId { get; set; }

        [Required]
        public int ItemId { get; set; }

        [Required]
        [Range(0.1, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        public double Quantity { get; set; }

        public int PODetailId { get; set; }

        public string SuppDeliveryNote { get; set; }

        [Required]
        public string RefNo { get; set; }

        [Required]
        public int InNo { get; set; }

        public DateTime? Date { get; set; }

        public int SiteId { get; set; }
    }
}
