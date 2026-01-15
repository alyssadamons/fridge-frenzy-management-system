using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductId { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public decimal Price { get; set; }

        public string Description { get; set; }
        public string Image { get; set; }

        [DisplayName("Category")]
        public int? CategoryId { get; set; } // optional to select a category

        public Category Category { get; set; }
        public virtual ICollection<FridgeRegistration> FridgeRegistrations { get; set; }
        // Navigation property for CartItems
        public virtual ICollection<Cart> CartItems { get; set; }

        // Navigation property for OrderItems
        public virtual ICollection<OrderItem> OrderItems { get; set; }

    }
}