using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using E_Commerce.Areas.Dashboard.Models;

namespace E_Commerce.Areas.Dashboard.Models
{
    public class Employee
    {
        [Key]
        public int EmployeeID { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ContactNumber { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Job Title")]
        public string JobTitle { get; set; } = string.Empty;

        [StringLength(100)]
        public string Department { get; set; }

        [StringLength(100)]
        public string Position { get; set; } = "Employee";

        [StringLength(100)]
        public string GeneratedPassword { get; set; }

        // FIXED: Only keep IdentityUserId (string) - remove UserId (int)
        public string IdentityUserId { get; set; }

        [StringLength(20)]
        public string Color { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        [NotMapped]
        public string LoginCredentials => $"Email: {Email ?? "Not set"} | Password: {GeneratedPassword ?? "Not set"}";

        public virtual ICollection<Appointment> Appointments { get; set; }
    }
}