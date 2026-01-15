using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Areas.Identity.ViewModels
{
    public class CustomerAppointmentViewModel
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

        // For displaying available time slots
        public List<DateTime> AvailableSlots { get; set; }
    }
}