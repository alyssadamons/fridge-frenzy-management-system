using System;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Areas.Identity.ViewModels
{
    public class FridgeRegistrationViewModel
    {
        [Required]
        [Display(Name = "Fridge Model/Name")]
        [StringLength(200)]
        public string FridgeName { get; set; }

        [Display(Name = "Serial Number")]
        [StringLength(100)]
        public string SerialNumber { get; set; }

        // Make Brand optional
        [Display(Name = "Brand (Optional)")]
        [StringLength(100)]
        public string Brand { get; set; }

        [Display(Name = "Purchase Date")]
        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }

        [Display(Name = "Warranty End Date")]
        [DataType(DataType.Date)]
        public DateTime? WarrantyEndDate { get; set; }

        [Display(Name = "Installation Date")]
        [DataType(DataType.Date)]
        public DateTime? InstallationDate { get; set; }

        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string Notes { get; set; }

        // For registration from order
        public int? OrderId { get; set; }
        public int? ProductId { get; set; }
        public int? OrderItemId { get; set; }
    }
}