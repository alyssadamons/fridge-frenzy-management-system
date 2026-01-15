using E_Commerce.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.ViewModels
{
    public class CheckoutViewModel
    {
        public List<Cart> CartItems { get; set; } = new List<Cart>();
        public decimal SubTotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Total { get; set; }
        public string UserEmail { get; set; }

        // Customer info (will be auto-filled from database)
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }

        [Display(Name = "Special Instructions")]
        public string SpecialInstructions { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; }
    }
}