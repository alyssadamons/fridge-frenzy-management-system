using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace E_Commerce.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int NumericId { get; set; } // New integer ID for business logic

        [Display(Name = "Owner Name")]
        public string? Owner { get; set; }

        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [Display(Name = "Account Type")]
        public AccountType AccountType { get; set; } = AccountType.Individual;

        [Display(Name = "Street Number")]
        public string? StreetNumber { get; set; }

        [Display(Name = "Street Name")]
        public string? StreetName { get; set; }

        [Display(Name = "Suburb")]
        public string? Suburb { get; set; }

        [Display(Name = "City")]
        public string? City { get; set; }

        [Display(Name = "Postal Code")]
        public string? PostalCode { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }
    }

    public enum AccountType
    {
        Individual,
        Business
    }
}