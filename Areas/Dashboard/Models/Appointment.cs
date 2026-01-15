using E_Commerce.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Areas.Dashboard.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public int CustomerID { get; set; }

        [ForeignKey("CustomerID")]
        public Customer Customer { get; set; }

        
        public int? EmployeeID { get; set; }

        [ForeignKey("EmployeeID")]
        public Employee Employee { get; set; } 

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        public string IssueType { get; set; } 

        public string OtherIssue { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // New fields
        [MaxLength(1000)]
        public string? TechnicianFaults { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [Required]
        public bool IsDeleted { get; set; } = false;

        public string? Color { get; set; } 

        public int? FridgeId { get; set; }

        [ForeignKey("FridgeId")]
        public virtual FridgeRegistration Fridge { get; set; }
    }
}
