using E_Commerce.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Areas.Dashboard.Models
{
    public class Customer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CustomerID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Company Name")]
        public string Name { get; set; }

        [StringLength(100)]
        [Display(Name = "Owner Name")]
        public string Owner { get; set; }

        [Required]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Enter a valid 10-digit phone number starting with 0.")]
        [Display(Name = "Contact Number")]
        public string ContactNumber { get; set; }

        [EmailAddress]
        [StringLength(100)]
        [Required]
        [Display(Name = "Company Email")]
        public string Email { get; set; }

        [StringLength(10)]
        [Display(Name = "Street Number")]
        public string StreetNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Street Name")]
        public string StreetName { get; set; }

        [StringLength(100)]
        public string Suburb { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        [StringLength(10)]
        [Display(Name = "Postal Code")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "Postal code must be exactly 4 digits.")]
        public string PostalCode { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        // REMOVE [Required] attribute to make it optional for admin-created customers
        public string IdentityUserId { get; set; }

        [ForeignKey("IdentityUserId")]
        public ApplicationUser User { get; set; }

        public ICollection<FridgeRegistration> FridgeRegistrations { get; set; }
        public ICollection<Appointment> Appointments { get; set; }
    }
}