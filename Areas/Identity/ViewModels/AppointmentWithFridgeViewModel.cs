using System.ComponentModel.DataAnnotations;
using E_Commerce.Models;
using System.Collections.Generic;

namespace E_Commerce.Areas.Identity.ViewModels
{
    public class AppointmentWithFridgeViewModel
    {
        [Required(ErrorMessage = "Please select an issue type")]
        [Display(Name = "Issue Type")]
        public string IssueType { get; set; }

        [Display(Name = "Other Issue Description")]
        public string OtherIssue { get; set; }

        [Required(ErrorMessage = "Please select a preferred date and time")]
        [Display(Name = "Preferred Date & Time")]
        [DataType(DataType.DateTime)]
        public DateTime PreferredDateTime { get; set; }

        [Display(Name = "Additional Notes")]
        [StringLength(1000)]
        public string Notes { get; set; }

        // Fridge selection
        [Display(Name = "Select Fridge")]
        public int? SelectedFridgeId { get; set; }

        [Display(Name = "Or specify fridge model")]
        public string OtherFridgeModel { get; set; }

        // ADD THIS PROPERTY - This was missing!
        [Display(Name = "Brand")]
        public string OtherFridgeBrand { get; set; }

        // For displaying available time slots
        public List<DateTime> AvailableSlots { get; set; }

        // Customer's registered fridges
        public List<FridgeRegistration> CustomerFridges { get; set; } = new List<FridgeRegistration>();
    }
}