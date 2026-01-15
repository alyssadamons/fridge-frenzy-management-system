using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Dashboard.Services;
using E_Commerce.Models;
using E_Commerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Data;

namespace E_Commerce.Areas.Dashboard.Controllers
{
    [Area("Dashboard")]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILoggingService _loggingService;
        private readonly IEmployeeService _employeeService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EmployeeController> _logger;

        public EmployeeController(
            ApplicationDbContext context,
            ILoggingService loggingService,
            IEmployeeService employeeService,
            UserManager<ApplicationUser> userManager,
            ILogger<EmployeeController> logger)
        {
            _context = context;
            _loggingService = loggingService;
            _employeeService = employeeService;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Dashboard/Employee - FIXED with proper filtering and sorting
        public async Task<IActionResult> Index(string searchName, string searchJobTitle, string sortOrder)
        {
            // Set ViewData for form persistence
            ViewData["SearchName"] = searchName;
            ViewData["SearchJobTitle"] = searchJobTitle;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParam"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["JobTitleSortParam"] = sortOrder == "JobTitle" ? "jobtitle_desc" : "JobTitle";

            try
            {
                // Start with base query
                var employeesQuery = _context.Employees
                    .Where(e => e.IsActive)
                    .AsQueryable();

                // Apply search filters
                if (!string.IsNullOrEmpty(searchName))
                {
                    employeesQuery = employeesQuery.Where(e =>
                        (e.FirstName != null && e.FirstName.Contains(searchName)) ||
                        (e.LastName != null && e.LastName.Contains(searchName)));
                }

                if (!string.IsNullOrEmpty(searchJobTitle))
                {
                    employeesQuery = employeesQuery.Where(e =>
                        e.JobTitle != null && e.JobTitle.Contains(searchJobTitle));
                }

                // Apply sorting
                employeesQuery = sortOrder switch
                {
                    "name_desc" => employeesQuery.OrderByDescending(e => e.LastName),
                    "JobTitle" => employeesQuery.OrderBy(e => e.JobTitle),
                    "jobtitle_desc" => employeesQuery.OrderByDescending(e => e.JobTitle),
                    _ => employeesQuery.OrderBy(e => e.LastName) // Default sort
                };

                // Execute query and handle NULL values safely
                var employees = await employeesQuery.ToListAsync();

                var safeEmployees = employees.Select(e => new Employee
                {
                    EmployeeID = e.EmployeeID,
                    FirstName = e.FirstName ?? "N/A",
                    LastName = e.LastName ?? "N/A",
                    ContactNumber = e.ContactNumber ?? "N/A",
                    Email = e.Email ?? "N/A",
                    JobTitle = e.JobTitle ?? "N/A",
                    Department = e.Department,
                    Position = e.Position ?? "Employee",
                    GeneratedPassword = e.GeneratedPassword,
                    Color = e.Color,
                    Notes = e.Notes,
                    IsActive = e.IsActive,
                    IsDeleted = e.IsDeleted
                }).ToList();

                return View(safeEmployees);
            }
            catch (Exception ex)
            {
                // Log the error and return empty list
                await _loggingService.LogActionAsync(
                    "EmployeeIndexError",
                    $"Error loading employees: {ex.Message}",
                    User.Identity?.Name
                );

                // Return empty list to avoid breaking the page
                return View(new List<Employee>());
            }
        }

        // CREATE MODAL
        public IActionResult CreateModal()
        {
            var employee = new Employee();
            return PartialView("_CreateEmployeePartial", employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateModal(Employee employee)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Generate credentials
                    var (email, password) = _employeeService.GenerateCredentials(
                        employee.FirstName, employee.LastName, employee.Position);

                    employee.Email = email;
                    employee.GeneratedPassword = password;
                    employee.IsActive = true;

                    // Create Identity User with unique NumericId
                    var user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        NumericId = GenerateUniqueNumericId()
                    };

                    var result = await _userManager.CreateAsync(user, password);

                    if (result.Succeeded)
                    {
                        // Add to appropriate role based on position
                        var role = GetRoleFromPosition(employee.Position);
                        await _userManager.AddToRoleAsync(user, role);

                        // Store only IdentityUserId
                        employee.IdentityUserId = user.Id;

                        // SET ALL REQUIRED FIELDS WITH DEFAULT VALUES
                        employee.Department ??= "General";
                        employee.Color ??= "#007bff";
                        employee.Notes ??= "";
                        employee.IsDeleted = false;

                        // ==== FIX: MANUALLY GENERATE EmployeeID ====
                        var maxEmployeeId = await _context.Employees.MaxAsync(e => (int?)e.EmployeeID) ?? 0;
                        employee.EmployeeID = maxEmployeeId + 1;
                        // ===========================================

                        _context.Employees.Add(employee);
                        await _context.SaveChangesAsync();

                        await _loggingService.LogActionAsync(
                            "EmployeeAdded",
                            $"New {employee.Position} added: {employee.FirstName} {employee.LastName}. Login: {email}",
                            User.Identity?.Name
                        );

                        return Json(new
                        {
                            success = true,
                            credentials = new
                            {
                                email = email,
                                password = password
                            }
                        });
                    }
                    else
                    {
                        var errors = result.Errors.Select(e => e.Description).ToList();
                        // Log the specific errors for debugging
                        _logger.LogError("Identity user creation failed: {Errors}", string.Join(", ", errors));

                        return Json(new
                        {
                            success = false,
                            error = "Failed to create user account",
                            errors = errors
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Get the innermost exception for detailed error message
                    var innerException = ex;
                    while (innerException.InnerException != null)
                    {
                        innerException = innerException.InnerException;
                    }

                    _logger.LogError(ex, "Error creating employee");

                    return Json(new
                    {
                        success = false,
                        error = $"Error: {innerException.Message}"
                    });
                }
            }

            var modelStateErrors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return Json(new
            {
                success = false,
                error = "Validation failed",
                errors = modelStateErrors
            });
        }

        // Helper method to create employee with manual ID generation
        private async Task<IActionResult> CreateEmployeeWithManualId(Employee employee)
        {
            try
            {
                // Generate credentials
                var (email, password) = _employeeService.GenerateCredentials(
                    employee.FirstName, employee.LastName, employee.Position);

                employee.Email = email;
                employee.GeneratedPassword = password;
                employee.IsActive = true;

                // Create Identity User with unique NumericId
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    NumericId = GenerateUniqueNumericId()
                };

                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    // Add to appropriate role based on position
                    var role = GetRoleFromPosition(employee.Position);
                    await _userManager.AddToRoleAsync(user, role);

                    // Store only IdentityUserId
                    employee.IdentityUserId = user.Id;

                    // SET ALL REQUIRED FIELDS WITH DEFAULT VALUES
                    employee.Department ??= "General";
                    employee.Color ??= "#007bff";
                    employee.Notes ??= "";
                    employee.IsDeleted = false;

                    // MANUALLY generate EmployeeID
                    var maxEmployeeId = await _context.Employees.MaxAsync(e => (int?)e.EmployeeID) ?? 0;
                    employee.EmployeeID = maxEmployeeId + 1;

                    _context.Employees.Add(employee);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "EmployeeAdded",
                        $"New {employee.Position} added with manual ID: {employee.FirstName} {employee.LastName}. Login: {email}",
                        User.Identity?.Name
                    );

                    return Json(new
                    {
                        success = true,
                        credentials = new
                        {
                            email = email,
                            password = password
                        }
                    });
                }
                else
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return Json(new
                    {
                        success = false,
                        error = "Failed to create user account",
                        errors = errors
                    });
                }
            }
            catch (Exception ex)
            {
                var innerException = ex;
                while (innerException.InnerException != null)
                {
                    innerException = innerException.InnerException;
                }

                return Json(new
                {
                    success = false,
                    error = $"Error with manual ID: {innerException.Message}"
                });
            }
        }

        private int GenerateUniqueNumericId()
        {
            // Use a simple timestamp-based approach to avoid database queries
            return (int)(DateTime.Now.Ticks % 1000000);
        }

        // EDIT MODAL - Now allows editing email and password
        public async Task<IActionResult> EditModal(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null) return NotFound();

                // Ensure required fields are not null
                employee.Position ??= "Employee";
                employee.JobTitle ??= "Employee";

                return PartialView("_EditEmployeePartial", employee);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "EditModalError",
                    $"Error loading employee for edit: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditModal(int id, Employee employee)
        {
            if (id != employee.EmployeeID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingEmployee = await _context.Employees.FindAsync(id);
                    if (existingEmployee == null) return NotFound();

                    // Update employee details with NULL checks
                    existingEmployee.FirstName = employee.FirstName ?? existingEmployee.FirstName;
                    existingEmployee.LastName = employee.LastName ?? existingEmployee.LastName;
                    existingEmployee.ContactNumber = employee.ContactNumber ?? existingEmployee.ContactNumber;
                    existingEmployee.JobTitle = employee.JobTitle ?? existingEmployee.JobTitle;
                    existingEmployee.Position = employee.Position ?? existingEmployee.Position;
                    existingEmployee.IsActive = employee.IsActive;

                    // FIX: Update email and password using IdentityUserId (string)
                    if (!string.IsNullOrEmpty(employee.Email) && existingEmployee.Email != employee.Email && !string.IsNullOrEmpty(existingEmployee.IdentityUserId))
                    {
                        var user = await _userManager.FindByIdAsync(existingEmployee.IdentityUserId);
                        if (user != null)
                        {
                            user.Email = employee.Email;
                            user.UserName = employee.Email;
                            await _userManager.UpdateAsync(user);
                            existingEmployee.Email = employee.Email;
                        }
                    }

                    if (!string.IsNullOrEmpty(employee.GeneratedPassword) && !string.IsNullOrEmpty(existingEmployee.IdentityUserId))
                    {
                        var user = await _userManager.FindByIdAsync(existingEmployee.IdentityUserId);
                        if (user != null)
                        {
                            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                            var result = await _userManager.ResetPasswordAsync(user, token, employee.GeneratedPassword);
                            if (result.Succeeded)
                            {
                                existingEmployee.GeneratedPassword = employee.GeneratedPassword;
                            }
                        }
                    }

                    _context.Update(existingEmployee);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "EmployeeUpdated",
                        $"Employee updated: {employee.FirstName} {employee.LastName}",
                        User.Identity?.Name
                    );

                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error updating employee: {ex.Message}");
                }
            }
            return PartialView("_EditEmployeePartial", employee);
        }

        public async Task<IActionResult> ViewModal(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null) return NotFound();

                // Ensure required fields are not null for display
                employee.Position ??= "Employee";
                employee.JobTitle ??= "Employee";
                employee.FirstName ??= "N/A";
                employee.LastName ??= "N/A";
                employee.ContactNumber ??= "N/A";

                return PartialView("_ViewEmployeePartial", employee);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "ViewModalError",
                    $"Error loading employee for view: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        public async Task<IActionResult> DeleteModal(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null) return NotFound();

                // Ensure required fields are not null for display
                employee.FirstName ??= "N/A";
                employee.LastName ??= "N/A";

                return PartialView("_DeleteEmployeePartial", employee);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "DeleteModalError",
                    $"Error loading employee for delete: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteModalConfirmed(int id)
        {
            try
            {
                var employee = await _context.Employees.FindAsync(id);

                if (employee != null)
                {
                    // Check if employee has appointments
                    var appointmentCount = await _context.Appointments
                        .CountAsync(a => a.EmployeeID == id);

                    if (appointmentCount > 0)
                    {
                        // Don't delete, just mark as inactive
                        employee.IsActive = false;
                        employee.IsDeleted = true;
                        _context.Update(employee);

                        await _loggingService.LogActionAsync(
                            "EmployeeDeactivated",
                            $"Employee marked as inactive (has {appointmentCount} appointments): {employee.FirstName} {employee.LastName}",
                            User.Identity?.Name
                        );
                    }
                    else
                    {
                        // No appointments, safe to delete
                        _context.Employees.Remove(employee);

                        await _loggingService.LogActionAsync(
                            "EmployeeDeleted",
                            $"Employee deleted: {employee.FirstName} {employee.LastName}",
                            User.Identity?.Name
                        );
                    }

                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "DeleteError",
                    $"Error deleting employee: {ex.Message}. Stack: {ex.StackTrace}",
                    User.Identity?.Name
                );
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ========== REGULAR ACTIONS ==========

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Generate credentials
                    var (email, password) = _employeeService.GenerateCredentials(
                        employee.FirstName, employee.LastName, employee.Position);

                    employee.Email = email;
                    employee.GeneratedPassword = password;
                    employee.IsActive = true;

                    // Create Identity User
                    var user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user, password);

                    if (result.Succeeded)
                    {
                        // Add to appropriate role based on position
                        var role = GetRoleFromPosition(employee.Position);
                        await _userManager.AddToRoleAsync(user, role);

                        // Save employee with user ID
                        employee.IdentityUserId = user.Id;
                        _context.Employees.Add(employee);
                        await _context.SaveChangesAsync();

                        await _loggingService.LogActionAsync(
                            "EmployeeAdded",
                            $"New {employee.Position} added: {employee.FirstName} {employee.LastName}. Login: {email}",
                            User.Identity?.Name
                        );

                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error creating employee: {ex.Message}");
                }
            }
            return View(employee);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null) return NotFound();

                // Ensure required fields are not null
                employee.Position ??= "Employee";
                employee.JobTitle ??= "Employee";

                return View(employee);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "EditError",
                    $"Error loading employee for edit: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee employee)
        {
            if (id != employee.EmployeeID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingEmployee = await _context.Employees.FindAsync(id);
                    if (existingEmployee == null) return NotFound();

                    // Update employee details with NULL checks
                    existingEmployee.FirstName = employee.FirstName ?? existingEmployee.FirstName;
                    existingEmployee.LastName = employee.LastName ?? existingEmployee.LastName;
                    existingEmployee.ContactNumber = employee.ContactNumber ?? existingEmployee.ContactNumber;
                    existingEmployee.JobTitle = employee.JobTitle ?? existingEmployee.JobTitle;
                    existingEmployee.Position = employee.Position ?? existingEmployee.Position;
                    existingEmployee.IsActive = employee.IsActive;

                    // Update email and password if changed
                    if (!string.IsNullOrEmpty(employee.Email) && existingEmployee.Email != employee.Email && !string.IsNullOrEmpty(existingEmployee.IdentityUserId))
                    {
                        var user = await _userManager.FindByIdAsync(existingEmployee.IdentityUserId);
                        if (user != null)
                        {
                            user.Email = employee.Email;
                            user.UserName = employee.Email;
                            await _userManager.UpdateAsync(user);
                            existingEmployee.Email = employee.Email;
                        }
                    }

                    if (!string.IsNullOrEmpty(employee.GeneratedPassword) && !string.IsNullOrEmpty(existingEmployee.IdentityUserId))
                    {
                        var user = await _userManager.FindByIdAsync(existingEmployee.IdentityUserId);
                        if (user != null)
                        {
                            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                            var result = await _userManager.ResetPasswordAsync(user, token, employee.GeneratedPassword);
                            if (result.Succeeded)
                            {
                                existingEmployee.GeneratedPassword = employee.GeneratedPassword;
                            }
                        }
                    }

                    _context.Update(existingEmployee);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "EmployeeUpdated",
                        $"Employee updated: {employee.FirstName} {employee.LastName}",
                        User.Identity?.Name
                    );

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error updating employee: {ex.Message}");
                }
            }
            return View(employee);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null) return NotFound();

                // Ensure required fields are not null for display
                employee.Position ??= "Employee";
                employee.JobTitle ??= "Employee";
                employee.FirstName ??= "N/A";
                employee.LastName ??= "N/A";
                employee.ContactNumber ??= "N/A";

                return View(employee);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "DetailsError",
                    $"Error loading employee details: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee == null) return NotFound();

                // Ensure required fields are not null for display
                employee.FirstName ??= "N/A";
                employee.LastName ??= "N/A";

                return View(employee);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "DeleteViewError",
                    $"Error loading employee for delete view: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee != null)
                {
                    employee.IsActive = false;
                    _context.Update(employee);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "EmployeeDeleted",
                        $"Employee marked as inactive: {employee.FirstName} {employee.LastName}",
                        User.Identity?.Name
                    );
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "DeleteConfirmedError",
                    $"Error confirming employee deletion: {ex.Message}",
                    User.Identity?.Name
                );
                return RedirectToAction(nameof(Index));
            }
        }

        private string GetRoleFromPosition(string position)
        {
            return position?.ToLower() switch
            {
                "technician" => "Technician",
                "sales" => "Sales",
                "customermanager" or "customer manager" => "CustomerManager",
                _ => "Employee"
            };
        }
    }
}