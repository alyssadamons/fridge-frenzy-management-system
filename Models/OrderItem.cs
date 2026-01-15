using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        [ForeignKey("OrderId")]  // ADDED
        public Order Order { get; set; }

        public int ProductId { get; set; }

        [ForeignKey("ProductId")]  // ADDED
        public Product Product { get; set; }

        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}