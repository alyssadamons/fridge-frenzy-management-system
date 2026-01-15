using E_Commerce.Areas.Dashboard.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Commerce.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public int CustomerID { get; set; }

        [ForeignKey("CustomerID")]
        public Customer Customer { get; set; }

        [Required]
        public string UserId { get; set; }

        

        [Required]
        public DateTime OrderDate { get; set; }

        public decimal SubTotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Total { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; }

        [Required]
        [StringLength(500)]
        public string DeliveryAddress { get; set; }

        [Required]
        [StringLength(200)]
        public string CustomerName { get; set; }

        [Required]
        [StringLength(20)]
        public string CustomerPhone { get; set; }

        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        //Paypal Code
        public string PaymentMethod { get; set; }
        public string PaymentReference { get; set; }

    }
}