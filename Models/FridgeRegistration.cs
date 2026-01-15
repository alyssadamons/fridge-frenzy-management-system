using E_Commerce.Areas.Dashboard.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class FridgeRegistration
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Order")]
        public int? OrderId { get; set; }  // Nullable for temp fridges
        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }

        [Display(Name = "Product")]
        public int? ProductId { get; set; }  // Nullable for temp fridges
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        [StringLength(200)]
        [Display(Name = "Nickname")]
        public string Nickname { get; set; }

        [Required]
        [Display(Name = "Purchase Date")]
        public DateTime PurchaseDate { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Required]
        [StringLength(200)]
        [Display(Name = "Fridge Name")]
        public string FridgeName { get; set; } = "";

        [Display(Name = "Registration Date")]
        public DateTime RegistrationDate { get; set; } = DateTime.Now;

        
        [StringLength(100)]
        [Display(Name = "Brand")]
        public string Brand { get; set; }

        [StringLength(100)]
        [Display(Name = "Serial Number")]
        public string SerialNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string Notes { get; set; }

        [Display(Name = "Order Item")]
        public int? OrderItemId { get; set; }
        [ForeignKey("OrderItemId")]
        public virtual OrderItem OrderItem { get; set; }

        // Navigation properties
        public virtual ICollection<Appointment> Appointments { get; set; }
    }
}