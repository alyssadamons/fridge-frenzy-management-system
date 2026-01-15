using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace E_Commerce.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context; 

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext applicationContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = applicationContext; 
        }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public string Username { get; set; }

        public class InputModel
        {
            [Display(Name = "Owner Name")]
            [RegularExpression(@"^[a-zA-Z\s\-]+$", ErrorMessage = "Owner name can only contain letters, spaces, and hyphens.")]
            [StringLength(100, ErrorMessage = "Owner name cannot exceed 100 characters.")]
            public string Owner { get; set; }

            [Display(Name = "Company Name")]
            [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters.")]
            public string CompanyName { get; set; }

            [Phone]
            [Display(Name = "Contact Number")]
            [RegularExpression(@"^0\d{9}$", ErrorMessage = "Enter a valid 10-digit phone number starting with 0.")]
            public string ContactNumber { get; set; }

            [StringLength(10, ErrorMessage = "Street number cannot exceed 10 characters.")]
            public string StreetNumber { get; set; }

            [StringLength(100, ErrorMessage = "Street name cannot exceed 100 characters.")]
            [RegularExpression(@"^[a-zA-Z\s\-\.']+$", ErrorMessage = "Street name can only contain letters, spaces, hyphens, periods, and apostrophes.")]
            public string StreetName { get; set; }

            [StringLength(100, ErrorMessage = "Suburb cannot exceed 100 characters.")]
            [RegularExpression(@"^[a-zA-Z\s\-]+$", ErrorMessage = "Suburb can only contain letters, spaces, and hyphens.")]
            public string Suburb { get; set; }

            [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
            [RegularExpression(@"^[a-zA-Z\s\-]+$", ErrorMessage = "City can only contain letters, spaces, and hyphens.")]
            public string City { get; set; }

            [StringLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
            [RegularExpression(@"^\d{4}$", ErrorMessage = "Postal code must be exactly 4 digits.")]
            public string PostalCode { get; set; }

            [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
            public string Notes { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            Username = await _userManager.GetUserNameAsync(user);

            // Look up customer by IdentityUserId (GUID)
            var customer = await _context.Customers // CHANGED from _context
                .FirstOrDefaultAsync(c => c.IdentityUserId == user.Id);

            Input = new InputModel
            {
                Owner = customer?.Owner,
                CompanyName = customer?.Name,
                ContactNumber = customer?.ContactNumber,
                StreetNumber = customer?.StreetNumber,
                StreetName = customer?.StreetName,
                Suburb = customer?.Suburb,
                City = customer?.City,
                PostalCode = customer?.PostalCode,
                Notes = customer?.Notes
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            // Look up customer by IdentityUserId (GUID)
            var customer = await _context.Customers // CHANGED from _context
                .FirstOrDefaultAsync(c => c.IdentityUserId == user.Id);

            if (customer == null)
            {
                customer = new Customer
                {
                    IdentityUserId = user.Id,
                    Name = Input.CompanyName,
                    Owner = Input.Owner,
                    Email = user.Email, // Add email
                    ContactNumber = Input.ContactNumber,
                    StreetNumber = Input.StreetNumber,
                    StreetName = Input.StreetName,
                    Suburb = Input.Suburb,
                    City = Input.City,
                    PostalCode = Input.PostalCode,
                    Notes = Input.Notes,
                    IsActive = true,
                    IsDeleted = false
                };
                _context.Customers.Add(customer); // CHANGED from _context
            }
            else
            {
                // Update existing customer
                customer.Owner = Input.Owner;
                customer.Name = Input.CompanyName;
                customer.ContactNumber = Input.ContactNumber;
                customer.StreetNumber = Input.StreetNumber;
                customer.StreetName = Input.StreetName;
                customer.Suburb = Input.Suburb;
                customer.City = Input.City;
                customer.PostalCode = Input.PostalCode;
                customer.Notes = Input.Notes;
            }

            await _context.SaveChangesAsync(); // CHANGED from _context
            StatusMessage = "Your profile has been updated";

            await _signInManager.RefreshSignInAsync(user);
            return RedirectToPage();
        }
    }
}