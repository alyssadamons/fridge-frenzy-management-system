using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Spreadsheet;
using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Models;
using E_Commerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using E_Commerce.Data;

namespace E_Commerce.Areas.Dashboard.Controllers
{
    [Area("Dashboard")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILoggingService _loggingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AppointmentsController> _logger;

        public CustomerController(
            ApplicationDbContext context,
            ILoggingService loggingService,
            UserManager<ApplicationUser> userManager,
            ILogger<AppointmentsController> logger)
        {
            _context = context;
            _loggingService = loggingService;
            _userManager = userManager;
            _logger = logger;
        }

        // ======================
        // INDEX - Active Customers
        // ======================
        public async Task<IActionResult> Index(string sortOrder, string searchName, string searchOwner)
        {
            ViewData["NameSortParam"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["OwnerSortParam"] = sortOrder == "owner" ? "owner_desc" : "owner";
            ViewData["LocationSortParam"] = sortOrder == "location" ? "location_desc" : "location";

            ViewData["SearchName"] = searchName;
            ViewData["SearchOwner"] = searchOwner;

            var customers = _context.Customers.AsQueryable();

            // Apply search filters
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                customers = customers.Where(c => c.Name.Contains(searchName));
            }

            if (!string.IsNullOrWhiteSpace(searchOwner))
            {
                customers = customers.Where(c => c.Owner.Contains(searchOwner));
            }

            // IMPORTANT: Only show active, non-deleted customers
            customers = customers.Where(c => !c.IsDeleted && c.IsActive);

            // Apply sorting
            customers = sortOrder switch
            {
                "name_desc" => customers.OrderByDescending(c => c.Name),
                "owner" => customers.OrderBy(c => c.Owner),
                "owner_desc" => customers.OrderByDescending(c => c.Owner),
                "location" => customers.OrderBy(c => c.City),
                "location_desc" => customers.OrderByDescending(c => c.City),
                _ => customers.OrderBy(c => c.Name)
            };

            return View(await customers.ToListAsync());
        }

        // ======================
        // CREATE MODAL
        // ======================
        public IActionResult CreateModal() => PartialView("_CreateCustomerPartial");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateModal(Customer customer)
        {
            try
            {
                Console.WriteLine("=== CREATE CUSTOMER STARTED ===");
                Console.WriteLine($"ModelState IsValid: {ModelState.IsValid}");

                // Log model state errors
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    Console.WriteLine($"ModelState errors: {string.Join(", ", errors)}");

                    return Json(new
                    {
                        success = false,
                        error = "Please fix the validation errors",
                        errors = errors
                    });
                }

                // Set default values
                customer.IsActive = true;
                customer.IsDeleted = false;

                // For admin-created customers, IdentityUserId can be null
                // They can create an account later if needed
                customer.IdentityUserId = null;

                Console.WriteLine($"Creating customer: {customer.Name}, Email: {customer.Email}");

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Customer created successfully with ID: {customer.CustomerID}");

                _logger.LogInformation("Admin created customer: {CustomerName} with ID: {CustomerId}",
                    customer.Name, customer.CustomerID);

                await _loggingService.LogActionAsync(
                    "CustomerAdded",
                    $"Admin added new customer: {customer.Name} (CustomerID: {customer.CustomerID})",
                    User.Identity?.Name,
                    customer.Email,
                    customer.Name
                );

                return Json(new
                {
                    success = true,
                    message = "Customer created successfully!",
                    customerId = customer.CustomerID
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating customer: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                _logger.LogError(ex, "Error creating customer");
                return Json(new
                {
                    success = false,
                    error = $"Error creating customer: {ex.Message}"
                });
            }
        }

        // ======================
        // EDIT MODAL
        // ======================
        public async Task<IActionResult> EditModal(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null) return NotFound();

                return PartialView("_EditCustomerPartial", customer);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "EditCustomerModalError",
                    $"Error loading customer for edit: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditModal(int id, Customer customer, string newPassword = null)
        {
            _logger.LogInformation("=== EditModal POST called for CustomerID: {CustomerId} ===", id);

            if (id != customer.CustomerID)
            {
                return Json(new { success = false, error = "Customer ID mismatch" });
            }

            // Log model state for debugging
            if (!ModelState.IsValid)
            {
                var modelErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("ModelState invalid. Errors: {Errors}", string.Join(", ", modelErrors));

                return Json(new
                {
                    success = false,
                    error = "Please fix the validation errors",
                    errors = modelErrors
                });
            }

            try
            {
                var existingCustomer = await _context.Customers.FindAsync(id);
                if (existingCustomer == null)
                {
                    return Json(new { success = false, error = "Customer not found" });
                }

                // Update customer details
                existingCustomer.Name = customer.Name ?? existingCustomer.Name;
                existingCustomer.Owner = customer.Owner ?? existingCustomer.Owner;
                existingCustomer.Email = customer.Email ?? existingCustomer.Email;
                existingCustomer.ContactNumber = customer.ContactNumber ?? existingCustomer.ContactNumber;
                existingCustomer.StreetNumber = customer.StreetNumber ?? existingCustomer.StreetNumber;
                existingCustomer.StreetName = customer.StreetName ?? existingCustomer.StreetName;
                existingCustomer.Suburb = customer.Suburb ?? existingCustomer.Suburb;
                existingCustomer.City = customer.City ?? existingCustomer.City;
                existingCustomer.PostalCode = customer.PostalCode ?? existingCustomer.PostalCode;
                existingCustomer.Notes = customer.Notes ?? existingCustomer.Notes;
                existingCustomer.IsActive = customer.IsActive;

                _context.Update(existingCustomer);
                await _context.SaveChangesAsync();

                // Handle password reset if a new password was provided
                string passwordResetMessage = "";
                bool passwordWasReset = false;

                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    _logger.LogInformation("Password reset requested for customer {Email}", existingCustomer.Email);

                    // Validate password meets requirements
                    if (newPassword.Length < 6)
                    {
                        return Json(new
                        {
                            success = false,
                            error = "Password must be at least 6 characters long"
                        });
                    }

                    // Find the user by IdentityUserId
                    var user = await _userManager.FindByIdAsync(existingCustomer.IdentityUserId);
                    if (user != null)
                    {
                        // Remove old password and set new one
                        var removeResult = await _userManager.RemovePasswordAsync(user);
                        if (removeResult.Succeeded)
                        {
                            var addResult = await _userManager.AddPasswordAsync(user, newPassword);
                            if (addResult.Succeeded)
                            {
                                passwordWasReset = true;
                                passwordResetMessage = " Password has been updated.";
                                _logger.LogInformation("Password reset successfully for customer {Email}", existingCustomer.Email);
                            }
                            else
                            {
                                var passwordErrors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                                return Json(new
                                {
                                    success = false,
                                    error = $"Failed to set new password: {passwordErrors}"
                                });
                            }
                        }
                        else
                        {
                            var removeErrors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                            return Json(new
                            {
                                success = false,
                                error = $"Failed to remove old password: {removeErrors}"
                            });
                        }
                    }
                    else
                    {
                        return Json(new
                        {
                            success = false,
                            error = "User account not found. Please contact support."
                        });
                    }
                }

                return Json(new
                {
                    success = true,
                    message = "Customer updated successfully" + passwordResetMessage,
                    tempPassword = passwordWasReset ? newPassword : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer {CustomerId}", id);
                return Json(new { success = false, error = $"Error: {ex.Message}" });
            }
        }

        // ======================
        // EDIT PAST CUSTOMER (For Inactive Customers)
        // ======================
        public async Task<IActionResult> EditPastCustomer(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null) return NotFound();

                return PartialView("_EditPastCustomerPartial", customer);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "EditPastCustomerModalError",
                    $"Error loading past customer for edit: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPastCustomer(int id, Customer customer, bool resetPassword = false)
        {
            if (id != customer.CustomerID)
            {
                return Json(new { success = false, error = "Customer ID mismatch" });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCustomer = await _context.Customers.FindAsync(id);
                    if (existingCustomer == null)
                    {
                        return Json(new { success = false, error = "Customer not found" });
                    }

                    // Update customer details with proper address fields
                    existingCustomer.Name = customer.Name ?? existingCustomer.Name;
                    existingCustomer.Owner = customer.Owner ?? existingCustomer.Owner;
                    existingCustomer.Email = customer.Email ?? existingCustomer.Email;
                    existingCustomer.ContactNumber = customer.ContactNumber ?? existingCustomer.ContactNumber;
                    existingCustomer.StreetNumber = customer.StreetNumber ?? existingCustomer.StreetNumber;
                    existingCustomer.StreetName = customer.StreetName ?? existingCustomer.StreetName;
                    existingCustomer.Suburb = customer.Suburb ?? existingCustomer.Suburb;
                    existingCustomer.City = customer.City ?? existingCustomer.City;
                    existingCustomer.PostalCode = customer.PostalCode ?? existingCustomer.PostalCode;
                    existingCustomer.Notes = customer.Notes ?? existingCustomer.Notes;

                    _context.Update(existingCustomer);
                    await _context.SaveChangesAsync();

                    // Handle password reset if requested
                    string tempPassword = null;
                    if (resetPassword)
                    {
                        tempPassword = await ResetUserPassword(existingCustomer.IdentityUserId, existingCustomer.Email, existingCustomer.Name);
                    }

                    await _loggingService.LogActionAsync(
                        "PastCustomerUpdated",
                        $"Past customer updated: {customer.Name}" + (resetPassword ? " with password reset" : ""),
                        User.Identity?.Name,
                        customer.Email,
                        customer.Name
                    );

                    // Return consistent response object
                    return Json(new
                    {
                        success = true,
                        message = "Customer updated successfully" + (resetPassword ? " and password was reset" : ""),
                        tempPassword = tempPassword // This will be null if no reset was requested
                    });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, error = $"Error updating customer: {ex.Message}" });
                }
            }

            // If we get here, there were validation errors
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return Json(new { success = false, error = "Validation failed", errors = errors });
        }

        // ======================
        // PASSWORD RESET HELPER METHOD
        // ======================
        private async Task<string> ResetUserPassword(string identityUserId, string email, string customerName)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(identityUserId);
                if (user == null)
                {
                    await _loggingService.LogActionAsync(
                        "PasswordResetError",
                        $"User not found for password reset: {identityUserId}",
                        User.Identity?.Name,
                        email,
                        customerName
                    );
                    return null;
                }

                // Generate a temporary password
                var tempPassword = GenerateTemporaryPassword();

                return await SetUserPassword(user, tempPassword, email, customerName);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "PasswordResetError",
                    $"Error during password reset: {ex.Message}",
                    User.Identity?.Name,
                    email,
                    customerName
                );
                return null;
            }
        }

        private async Task<string> SetUserPassword(string identityUserId, string newPassword, string email, string customerName)
        {
            var user = await _userManager.FindByIdAsync(identityUserId);
            return await SetUserPassword(user, newPassword, email, customerName);
        }

        private async Task<string> SetUserPassword(ApplicationUser user, string newPassword, string email, string customerName)
        {
            try
            {
                if (user == null)
                {
                    await _loggingService.LogActionAsync(
                        "PasswordResetError",
                        $"User not found for password reset",
                        User.Identity?.Name,
                        email,
                        customerName
                    );
                    return null;
                }

                // Remove current password and set new one
                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                {
                    await _loggingService.LogActionAsync(
                        "PasswordResetError",
                        $"Failed to remove old password: {string.Join(", ", removePasswordResult.Errors.Select(e => e.Description))}",
                        User.Identity?.Name,
                        email,
                        customerName
                    );
                    return null;
                }

                var addPasswordResult = await _userManager.AddPasswordAsync(user, newPassword);
                if (!addPasswordResult.Succeeded)
                {
                    await _loggingService.LogActionAsync(
                        "PasswordResetError",
                        $"Failed to set new password: {string.Join(", ", addPasswordResult.Errors.Select(e => e.Description))}",
                        User.Identity?.Name,
                        email,
                        customerName
                    );
                    return null;
                }

                await _loggingService.LogActionAsync(
                    "PasswordResetSuccess",
                    $"Password reset successfully for customer: {customerName}",
                    User.Identity?.Name,
                    email,
                    customerName
                );

                return newPassword;
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "PasswordResetError",
                    $"Error during password reset: {ex.Message}",
                    User.Identity?.Name,
                    email,
                    customerName
                );
                return null;
            }
        }

        // ======================
        // GENERATE TEMPORARY PASSWORD
        // ======================
        private string GenerateTemporaryPassword()
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            var random = new Random();
            var password = new StringBuilder();

            // Ensure at least one uppercase, one lowercase, one digit, and one special character
            password.Append((char)random.Next(65, 91)); // Uppercase
            password.Append((char)random.Next(97, 123)); // Lowercase
            password.Append((char)random.Next(48, 58)); // Digit
            password.Append("!@#$%^&*"[random.Next(0, 8)]); // Special character

            // Add remaining characters
            for (int i = 4; i < 12; i++)
            {
                password.Append(validChars[random.Next(validChars.Length)]);
            }

            // Shuffle the password
            return new string(password.ToString().OrderBy(x => random.Next()).ToArray());
        }

        // ======================
        // VIEW MODAL - Fixed Version
        // ======================
        // Replace your ViewModal method with this fixed version

        public async Task<IActionResult> ViewModal(int id)
        {
            try
            {
                _logger.LogInformation("=== ViewModal called for CustomerID: {CustomerId} ===", id);

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerID == id);

                if (customer == null)
                {
                    _logger.LogWarning("❌ Customer not found with ID: {CustomerId}", id);
                    return NotFound();
                }

                _logger.LogInformation("✅ Customer found: {CustomerName}", customer.Name);

                var now = DateTime.Now;

                // Get upcoming appointments - keep DateTime objects, don't convert to string yet
                var upcomingAppointments = await _context.Appointments
                    .Include(a => a.Employee)
                    .Where(a => a.CustomerID == id && !a.IsDeleted && a.StartTime >= now)
                    .OrderBy(a => a.StartTime)
                    .Select(a => new Dictionary<string, object>
                    {
                { "EmployeeName", a.Employee != null ? a.Employee.FirstName + " " + a.Employee.LastName : "Not Assigned" },
                { "StartTime", a.StartTime },  // Keep as DateTime
                { "EndTime", a.EndTime },      // Keep as DateTime
                { "IssueType", a.IssueType ?? "Not specified" },
                { "Notes", a.Notes ?? "" },
                { "Status", a.Status ?? "Pending" }
                    })
                    .ToListAsync();

                // Get past appointments
                var pastAppointments = await _context.Appointments
                    .Include(a => a.Employee)
                    .Where(a => a.CustomerID == id && (a.IsDeleted || a.EndTime < now))
                    .OrderByDescending(a => a.StartTime)
                    .Select(a => new Dictionary<string, object>
                    {
                { "EmployeeName", a.Employee != null ? a.Employee.FirstName + " " + a.Employee.LastName : "Not Assigned" },
                { "StartTime", a.StartTime },  // Keep as DateTime
                { "EndTime", a.EndTime },      // Keep as DateTime
                { "IssueType", a.IssueType ?? "Not specified" },
                { "Notes", a.Notes ?? "" },
                { "Status", a.Status ?? "Completed" }
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {UpcomingCount} upcoming and {PastCount} past appointments",
                    upcomingAppointments.Count, pastAppointments.Count);

                ViewBag.UpcomingAppointments = upcomingAppointments;
                ViewBag.PastAppointments = pastAppointments;

                _logger.LogInformation("✅ Successfully loaded ViewModal for customer {CustomerName}", customer.Name);
                return PartialView("_ViewCustomerPartial", customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR in ViewModal for CustomerID: {CustomerId}", id);
                return StatusCode(500, $"Error loading customer details: {ex.Message}");
            }
        }

        // ======================
        // DELETE MODAL
        // ======================
        public async Task<IActionResult> DeleteModal(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound();

            return PartialView("_DeleteCustomerPartial", customer);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteModalConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                customer.IsActive = false;
                customer.IsDeleted = true;
                _context.Update(customer);

                // FIX: Use CustomerID instead of UserId
                var upcomingAppointments = await _context.Appointments
                    .Where(a => a.CustomerID == id && a.StartTime >= DateTime.Now && !a.IsDeleted)
                    .ToListAsync();

                foreach (var appt in upcomingAppointments)
                {
                    appt.IsDeleted = true;
                    _context.Update(appt);
                }

                await _context.SaveChangesAsync();

                await _loggingService.LogActionAsync(
                    "CustomerDeleted",
                    $"Customer marked as deleted: {customer.Name}",
                    User.Identity?.Name,
                    customer.Email,
                    customer.Name
                );
            }

            return Json(new { success = true });
        }

        // ======================
        // INACTIVE / DELETED CUSTOMERS
        // ======================
        public async Task<IActionResult> Inactive(string searchName, string searchOwner)
        {
            ViewData["SearchName"] = searchName;
            ViewData["SearchOwner"] = searchOwner;

            var deletedCustomers = _context.Customers
                .Where(c => c.IsDeleted || c.IsActive == false)
                .AsQueryable();

            // Apply search filters
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                deletedCustomers = deletedCustomers.Where(c => c.Name.Contains(searchName));
            }

            if (!string.IsNullOrWhiteSpace(searchOwner))
            {
                deletedCustomers = deletedCustomers.Where(c => c.Owner.Contains(searchOwner));
            }

            var customers = await deletedCustomers
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(customers);
        }

        // ======================
        // RESTORE CUSTOMER
        // ======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound();

            customer.IsDeleted = false;
            customer.IsActive = true;
            _context.Update(customer);

            // FIX: Use CustomerID instead of UserId
            var deletedAppointments = await _context.Appointments
                .Where(a => a.CustomerID == id && a.IsDeleted && a.StartTime >= DateTime.Now)
                .ToListAsync();

            foreach (var appt in deletedAppointments)
            {
                appt.IsDeleted = false;
                _context.Update(appt);
            }

            await _context.SaveChangesAsync();

            await _loggingService.LogActionAsync(
                "CustomerRestored",
                $"Customer restored: {customer.Name}",
                User.Identity?.Name,
                customer.Email,
                customer.Name
            );

            return RedirectToAction(nameof(Inactive));
        }
        }
    }
