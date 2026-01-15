#nullable disable

using DocumentFormat.OpenXml.InkML;
using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace E_Commerce.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly ILoggingService _loggingService;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext applicationContext,
            ILoggingService loggingService)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = applicationContext;
            _loggingService = loggingService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Please confirm your email")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            [Display(Name = "Confirm Email")]
            [Compare("Email", ErrorMessage = "Email and confirmation email do not match.")]
            public string ConfirmEmail { get; set; }

            [Required(ErrorMessage = "Password is required")]
            [StringLength(100, ErrorMessage = "Password must be between {2} and {1} characters long.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&]).{8,}$",
                ErrorMessage = "Password must include uppercase, lowercase, number, and special character.")]
            public string Password { get; set; }

            [Required(ErrorMessage = "Please confirm your password")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; }

            [Required(ErrorMessage = "Company name is required")]
            [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters.")]
            [Display(Name = "Company Name")]
            public string Name { get; set; }

            [StringLength(100, ErrorMessage = "Owner name cannot exceed 100 characters.")]
            [Display(Name = "Owner Name")]
            [RegularExpression(@"^[a-zA-Z\s\-]+$", ErrorMessage = "Owner name can only contain letters, spaces, and hyphens.")]
            public string Owner { get; set; }

            [Required(ErrorMessage = "Contact number is required")]
            [RegularExpression(@"^0\d{9}$", ErrorMessage = "Enter a valid 10-digit phone number starting with 0.")]
            public string ContactNumber { get; set; }

            [StringLength(10, ErrorMessage = "Street number cannot exceed 10 characters.")]
            [Display(Name = "Street Number")]
            public string StreetNumber { get; set; }

            [StringLength(100, ErrorMessage = "Street name cannot exceed 100 characters.")]
            [Display(Name = "Street Name")]
            [RegularExpression(@"^[a-zA-Z\s\-\.']+$", ErrorMessage = "Street name can only contain letters, spaces, hyphens, periods, and apostrophes.")]
            public string StreetName { get; set; }

            [StringLength(100, ErrorMessage = "Suburb cannot exceed 100 characters.")]
            [RegularExpression(@"^[a-zA-Z\s\-]+$", ErrorMessage = "Suburb can only contain letters, spaces, and hyphens.")]
            public string Suburb { get; set; }

            [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
            [RegularExpression(@"^[a-zA-Z\s\-]+$", ErrorMessage = "City can only contain letters, spaces, and hyphens.")]
            public string City { get; set; }

            [StringLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
            [Display(Name = "Postal Code")]
            [RegularExpression(@"^\d{4}$", ErrorMessage = "Postal code must be exactly 4 digits.")]
            public string PostalCode { get; set; }

            [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
            public string Notes { get; set; }

            [Required(ErrorMessage = "You must agree to the Privacy Policy before registering.")]
            [Display(Name = "I agree to the Privacy Policy")]
            public bool AgreeToTerms { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // Custom email uniqueness validation - ONLY CHECK DASHBOARD CONTEXT NOW
            if (!string.IsNullOrEmpty(Input.Email))
            {
                var existingUser = await _userManager.FindByEmailAsync(Input.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Input.Email", "An account with this email already exists. Please sign in or use a different email.");
                }

                // Check DashboardDbContext for existing email (single source of truth)
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email == Input.Email && !c.IsDeleted);

                if (existingCustomer != null)
                {
                    ModelState.AddModelError("Input.Email", "This email is already registered as a customer. Please sign in or use a different email.");
                }
            }

            // Validate terms agreement
            if (!Input.AgreeToTerms)
            {
                ModelState.AddModelError("Input.AgreeToTerms", "You must agree to the Terms of Service and Privacy Policy.");
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                // Set UserName to email (required by Identity)
                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                // Set additional user properties
                user.CompanyName = Input.Name;
                user.Owner = Input.Owner;
                user.ContactNumber = Input.ContactNumber;
                user.StreetNumber = Input.StreetNumber;
                user.StreetName = Input.StreetName;
                user.Suburb = Input.Suburb;
                user.City = Input.City;
                user.PostalCode = Input.PostalCode;
                user.Notes = Input.Notes;

                // Generate NumericId BEFORE creating user
                user.NumericId = await GenerateUniqueNumericIdAsync();

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");
                    await _userManager.AddClaimAsync(user, new Claim("NumericId", user.NumericId.ToString()));

                    // LOG USER REGISTRATION
                    await _loggingService.LogActionAsync(
                        "UserCreated",
                        "New user registered successfully",
                        Input.Email,
                        Input.Email,
                        Input.Name
                    );

                    // ✅ FIXED: Create customer in DashboardDbContext ONLY (single insert)
                    try
                    {
                        var customer = new Customer
                        {
                            Name = Input.Name,
                            Owner = Input.Owner,
                            ContactNumber = Input.ContactNumber,
                            Email = Input.Email,
                            StreetNumber = Input.StreetNumber,
                            StreetName = Input.StreetName,
                            Suburb = Input.Suburb,
                            City = Input.City,
                            PostalCode = Input.PostalCode,
                            Notes = Input.Notes,
                            IsActive = true,
                            IsDeleted = false,
                            IdentityUserId = user.Id
                        };

                        // Add to DashboardDbContext ONLY
                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Customer record created for user {UserId} with CustomerID {CustomerId}",
                            user.Id, customer.CustomerID);

                        // LOG CUSTOMER CREATION
                        await _loggingService.LogActionAsync(
                            "CustomerAdded",
                            $"Customer record created during user registration - Name: {customer.Name}, Email: {customer.Email}, CustomerID: {customer.CustomerID}",
                            Input.Email,
                            Input.Email,
                            Input.Name
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create customer record for user {UserId}. Error: {ErrorMessage}", user.Id, ex.Message);

                        if (ex.InnerException != null)
                        {
                            _logger.LogError("Inner Exception: {InnerException}", ex.InnerException.Message);
                        }

                        // Rollback user creation if customer creation fails
                        await _userManager.DeleteAsync(user);
                        ModelState.AddModelError(string.Empty, "An error occurred during registration. Please try again.");
                        return Page();
                    }

                    // Assign to User role
                    try
                    {
                        await _userManager.AddToRoleAsync(user, "User");
                        _logger.LogInformation("User assigned to User role.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to assign user to User role. Continuing without role assignment.");
                    }

                    // Email confirmation
                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId, code, returnUrl },
                        protocol: Request.Scheme);

                    try
                    {
                        await _emailSender.SendEmailAsync(Input.Email, "Confirm your email - Fridge Frenzy",
                            $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <h2 style='color: #3788d8;'>Welcome to Fridge Frenzy!</h2>
                        <p>Thank you for registering with Fridge Frenzy. Please confirm your email address by clicking the button below:</p>
                        <p style='text-align: center; margin: 30px 0;'>
                            <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' 
                               style='background-color: #3788d8; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Confirm Email
                            </a>
                        </p>
                        <p>If you didn't create an account with Fridge Frenzy, please ignore this email.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='color: #666; font-size: 12px;'>
                            Fridge Frenzy - Your Cooling Solution<br>
                            Email: info@fridgefrenzy.com<br>
                            Phone: 081 028 6437
                        </p>
                    </div>");

                        _logger.LogInformation("Confirmation email sent to {Email}", Input.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send confirmation email to {Email}", Input.Email);
                    }

                    // Set success message in TempData
                    TempData["SuccessMessage"] = $"Registration successful! Welcome to Fridge Frenzy, {Input.Name}!";
                    TempData["ShowSuccessDelay"] = "true";

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl ?? "/");
                }

                foreach (var error in result.Errors)
                {
                    var errorMessage = error.Description;
                    if (error.Code == "DuplicateUserName")
                    {
                        errorMessage = "An account with this email already exists. Please sign in or use a different email.";
                    }
                    ModelState.AddModelError(string.Empty, errorMessage);
                }
            }

            return Page();
        }

        private async Task<int> GenerateUniqueNumericIdAsync()
        {
            var maxUserNumericId = await _userManager.Users
                .OrderByDescending(u => u.NumericId)
                .Select(u => u.NumericId)
                .FirstOrDefaultAsync();

            return maxUserNumericId > 0 ? maxUserNumericId + 1 : 1;
        }

        public async Task<JsonResult> OnPostCheckEmailAsync([FromBody] EmailCheckRequest request)
        {
            if (string.IsNullOrEmpty(request?.Email))
            {
                return new JsonResult(new { valid = false, message = "Email is required" });
            }

            var existingUser = await _userManager.FindByEmailAsync(request.Email);

            // Check DashboardDbContext ONLY (single source of truth)
            var existingCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == request.Email && !c.IsDeleted);

            if (existingUser != null || existingCustomer != null)
            {
                return new JsonResult(new
                {
                    valid = false,
                    message = "This email is already registered. Please sign in or use a different email."
                });
            }

            return new JsonResult(new { valid = true, message = "Email is available" });
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. Ensure it has a parameterless constructor.");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
                throw new NotSupportedException("The default UI requires a user store with email support.");

            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }

    public class EmailCheckRequest
    {
        public string Email { get; set; }
    }
}