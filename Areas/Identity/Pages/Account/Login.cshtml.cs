// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using E_Commerce.Models;
using E_Commerce.Services;

namespace E_Commerce.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly ILoggingService _loggingService;

        public LoginModel(SignInManager<ApplicationUser> signInManager,
                         UserManager<ApplicationUser> userManager,
                         ILogger<LoginModel> logger,
                         ILoggingService loggingService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _loggingService = loggingService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address")]
            [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Password is required")]
            [DataType(DataType.Password)]
            [StringLength(100, ErrorMessage = "Password must be between {2} and {1} characters long.", MinimumLength = 6)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // Check if user exists first
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null)
                {
                    // User doesn't exist
                    await _loggingService.LogActionAsync(
                        "LoginFailed",
                        "Login attempt with non-existent email",
                        Input.Email
                    );

                    ModelState.AddModelError(string.Empty, "No account found with this email address. Please check your email or register for a new account.");
                    return Page();
                }

                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    // Add NumericId claim if not already present
                    var existingClaims = await _userManager.GetClaimsAsync(user);
                    var numericIdClaim = existingClaims.FirstOrDefault(c => c.Type == "NumericId");

                    if (numericIdClaim == null)
                    {
                        await _userManager.AddClaimAsync(user, new Claim("NumericId", user.NumericId.ToString()));
                        _logger.LogInformation("Added NumericId claim for user: {NumericId}", user.NumericId);
                    }

                    // Check if user is admin and redirect to dashboard
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        _logger.LogInformation("Admin user detected, redirecting to dashboard.");
                        return LocalRedirect("~/Dashboard/Home/Index");
                    }

                    // LOG USER LOGIN
                    await _loggingService.LogActionAsync(
                        "UserLogin",
                        "User logged in successfully",
                        Input.Email
                    );

                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    // User exists but password is wrong
                    await _loggingService.LogActionAsync(
                        "LoginFailed",
                        "Failed login attempt - incorrect password",
                        Input.Email
                    );

                    ModelState.AddModelError(string.Empty, "Incorrect password. Please try again or use 'Forgot Password' to reset it.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}