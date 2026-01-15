using System;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Areas.Identity.ViewModels
{
    public class RescheduleAppointmentViewModel
    {
        public int AppointmentId { get; set; }
        public string Status { get; set; }

        [Required(ErrorMessage = "Please select an issue type")]
        [Display(Name = "Issue Type")]
        public string IssueType { get; set; }

        [Required(ErrorMessage = "Please select a preferred date and time")]
        [Display(Name = "Preferred Date & Time")]
        [DataType(DataType.DateTime)]
        public DateTime PreferredDateTime { get; set; }

        [Display(Name = "Additional Notes")]
        [StringLength(1000)]
        public string Notes { get; set; }
    }
}