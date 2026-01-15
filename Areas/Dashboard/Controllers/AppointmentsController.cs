using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Areas.Dashboard.ViewModels;
using E_Commerce.Data;
using E_Commerce.Services;
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace E_Commerce.Areas.Dashboard.Controllers
{
    [Area("Dashboard")]
    [Authorize]
    

    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        
        private readonly ILoggingService _loggingService;
        private readonly ILogger<AppointmentsController> _logger;

        public AppointmentsController(ApplicationDbContext context, ILoggingService loggingService, ILogger<AppointmentsController> logger)
        {
            _context = context;
            
            _loggingService = loggingService;
            _logger = logger;
        }

        #region Index / Calendar

        [HttpGet]
        public async Task<IActionResult> Index(string search, string filter, bool showDeleted = false, string statusFilter = "all")
        {
            try
            {
                // AUTO-COMPLETE PAST APPOINTMENTS - Run this first
                await AutoCompletePastAppointments();

                // Start with base query
                var query = _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Employee)
                    .AsQueryable();

                // Debug: Check total count before filtering
                var totalCount = await query.CountAsync();
                Console.WriteLine($"Total appointments in database: {totalCount}");

                // Only show non-deleted and future appointments by default
                if (!showDeleted)
                {
                    query = query.Where(a => !a.IsDeleted && a.EndTime >= DateTime.Now);
                    var filteredCount = await query.CountAsync();
                    Console.WriteLine($"After non-deleted filter: {filteredCount}");
                }

                // FIXED: Status filter implementation
                if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "all")
                {
                    query = query.Where(a => a.Status == statusFilter);
                    var statusCount = await query.CountAsync();
                    Console.WriteLine($"After status filter '{statusFilter}': {statusCount}");
                }

                // FIXED: Search filter with proper parameter name
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = filter switch
                    {
                        "customer" => query.Where(a => a.Customer != null && a.Customer.Name.Contains(search)),
                        "technician" => query.Where(a => a.Employee != null &&
                            (a.Employee.FirstName.Contains(search) || a.Employee.LastName.Contains(search))),
                        "issue" => query.Where(a => a.IssueType.Contains(search)),
                        _ => query.Where(a =>
                            (a.Customer != null && a.Customer.Name.Contains(search)) ||
                            (a.Employee != null && (a.Employee.FirstName.Contains(search) || a.Employee.LastName.Contains(search))) ||
                            a.IssueType.Contains(search))
                    };
                    var searchCount = await query.CountAsync();
                    Console.WriteLine($"After search '{search}': {searchCount}");
                }

                // Order by start time
                query = query.OrderBy(a => a.StartTime);

                // FIXED: Correct ViewBag property names
                ViewBag.SearchString = search; // Changed from ViewBag.Search
                ViewBag.FilterBy = filter;     // Changed from ViewBag.Filter
                ViewBag.ShowDeleted = showDeleted;
                ViewBag.StatusFilter = statusFilter;

                // Get counts for status badges
                try
                {
                    ViewBag.PendingCount = await _context.Appointments.CountAsync(a => !a.IsDeleted && a.Status == "Pending" && a.EndTime >= DateTime.Now);
                    ViewBag.IncompleteCount = await _context.Appointments.CountAsync(a => !a.IsDeleted && a.Status == "Incomplete" && a.EndTime >= DateTime.Now);
                    ViewBag.CompletedCount = await _context.Appointments.CountAsync(a => !a.IsDeleted && a.Status == "Completed" && a.EndTime >= DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting appointment counts");
                    ViewBag.PendingCount = 0;
                    ViewBag.IncompleteCount = 0;
                    ViewBag.CompletedCount = 0;
                }

                var appointments = await query.ToListAsync();
                Console.WriteLine($"Final appointments to display: {appointments.Count}");

                return View(appointments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading appointments in Index method");
                TempData["Error"] = "An error occurred while loading appointments.";
                return View(new List<Appointment>());
            }
        }

     
        private async Task AutoCompletePastAppointments()
        {
            try
            {
                var pastAppointments = await _context.Appointments
                    .Where(a => !a.IsDeleted && a.EndTime < DateTime.Now && a.Status != "Completed" && a.Status != "Cancelled")
                    .ToListAsync();

                if (pastAppointments.Any())
                {
                    foreach (var appointment in pastAppointments)
                    {
                        appointment.Status = "Completed";
                        appointment.Color = "#28a745"; // Green for completed
                        _context.Update(appointment);

                        // Log the auto-completion
                        await _loggingService.LogActionAsync(
                            "AppointmentAutoCompleted",
                            $"Appointment auto-completed: {appointment.Customer?.Name} - {appointment.IssueType} (Past due)",
                            User.Identity?.Name,
                            appointment.Customer?.Email,
                            appointment.Customer?.Name
                        );
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Auto-completed {pastAppointments.Count} past appointments");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-completing past appointments");
            }
        }



        #endregion

        #region Cancel Appointment

        // POST: Cancel appointment (permanent removal)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int id, string cancellationReason = "")
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (appointment == null)
                {
                    return Json(new { success = false, error = "Appointment not found" });
                }

                // Log before deletion
                await _loggingService.LogActionAsync(
                    "AppointmentCancelled",
                    $"Appointment cancelled: {appointment.Customer?.Name} with {appointment.Employee?.FirstName} {appointment.Employee?.LastName} on {appointment.StartTime:dd MMM yyyy}. Reason: {cancellationReason}",
                    User.Identity?.Name,
                    appointment.Customer?.Email,
                    appointment.Customer?.Name
                );

                // Remove the appointment completely from database
                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Appointment cancelled and removed permanently!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling appointment");
                return Json(new { success = false, error = $"Error cancelling appointment: {ex.Message}" });
            }
        }

        // GET: Cancel modal
        public async Task<IActionResult> CancelModal(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            return PartialView("_CancelAppointmentPartial", appointment);
        }

        #endregion

        [HttpGet]
        public async Task<IActionResult> Calendar()
        {
            await PopulateViewBags();
            return View();
        }

        private async Task PopulateViewBags()
        {
            // Only show active customers
            ViewBag.Customers = await _context.Customers
                .Where(c => !c.IsDeleted && c.IsActive)
                .ToListAsync();

            // FIXED: More flexible technician query
            var technicians = await _context.Employees
                .Where(e => e.IsActive && !e.IsDeleted &&
                       (e.Position == "Technician" ||
                        e.Position.Contains("Technician") ||
                        e.JobTitle == "Technician" ||
                        e.JobTitle.Contains("Technician")))
                .Select(e => new TechnicianViewModel
                {
                    EmployeeID = e.EmployeeID,
                    FullName = $"{e.FirstName} {e.LastName}"
                })
                .ToListAsync();

            // If still no technicians, get all active employees for debugging
            if (!technicians.Any())
            {
                _logger.LogWarning("No technicians found with Position='Technician'. Loading all active employees for debugging.");

                var allEmployees = await _context.Employees
                    .Where(e => e.IsActive && !e.IsDeleted)
                    .Select(e => new
                    {
                        e.EmployeeID,
                        e.FirstName,
                        e.LastName,
                        e.Position,
                        e.JobTitle
                    })
                    .ToListAsync();

                _logger.LogInformation($"Available employees: {string.Join(", ", allEmployees.Select(e => $"{e.FirstName} {e.LastName} (Position: {e.Position}, JobTitle: {e.JobTitle})"))}");

                // Fallback: use all active employees
                technicians = allEmployees.Select(e => new TechnicianViewModel
                {
                    EmployeeID = e.EmployeeID,
                    FullName = $"{e.FirstName} {e.LastName}"
                }).ToList();
            }

            ViewBag.Technicians = technicians;

            // Debug logging
            _logger.LogInformation($"Loaded {technicians.Count} technicians for dropdown");
        }

        
        #region Create / Edit / Delete (Modals)

        // GET: Create modal
        public async Task<IActionResult> CreateModal()
        {
            await PopulateViewBags();
            return PartialView("_CreateAppointmentPartial");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateModal(Appointment appointment)
        {
            try
            {
                // FIX: Remove the problematic validation that was causing issues
                if (appointment.CustomerID <= 0)
                {
                    ModelState.AddModelError("CustomerID", "Customer is required.");
                }

                // Make EmployeeID optional for customer-created appointments
                if (appointment.Status == "Incomplete" && !appointment.EmployeeID.HasValue)
                {
                    // This is a customer-created appointment, technician can be assigned later
                }
                else if (!appointment.EmployeeID.HasValue || appointment.EmployeeID.Value <= 0)
                {
                    ModelState.AddModelError("EmployeeID", "Technician is required for this appointment type.");
                }

                // Handle "Other" issue type
                if (appointment.IssueType == "Other" && !string.IsNullOrWhiteSpace(appointment.OtherIssue))
                {
                    appointment.IssueType = appointment.OtherIssue;
                    appointment.OtherIssue = null; // Clear the OtherIssue field
                }

                // Set default end time if not provided
                if (appointment.EndTime == default)
                {
                    appointment.EndTime = appointment.StartTime.AddHours(1);
                }

                // Set default color if not provided
                if (string.IsNullOrEmpty(appointment.Color))
                {
                    appointment.Color = "#3788d8"; // Default blue
                }

                if (ModelState.IsValid)
                {
                    // Validate customer exists and is active
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.CustomerID == appointment.CustomerID && !c.IsDeleted && c.IsActive);

                    if (customer == null)
                    {
                        ModelState.AddModelError("CustomerID", "Selected customer is not active or does not exist.");
                        await PopulateViewBags();
                        return PartialView("_CreateAppointmentPartial", appointment);
                    }

                    // Validate employee exists if provided
                    if (appointment.EmployeeID.HasValue)
                    {
                        var employee = await _context.Employees
                            .FirstOrDefaultAsync(e => e.EmployeeID == appointment.EmployeeID.Value && e.IsActive && !e.IsDeleted);

                        if (employee == null)
                        {
                            ModelState.AddModelError("EmployeeID", "Selected technician is not active or does not exist.");
                            await PopulateViewBags();
                            return PartialView("_CreateAppointmentPartial", appointment);
                        }
                    }

                    _context.Add(appointment);
                    await _context.SaveChangesAsync();

                    // LOG APPOINTMENT CREATION
                    await _loggingService.LogActionAsync(
                        "AppointmentCreated",
                        $"New appointment created: {customer.Name} with technician ID {appointment.EmployeeID} on {appointment.StartTime:dd MMM yyyy}",
                        User.Identity?.Name,
                        customer.Email,
                        customer.Name
                    );

                    return Content("OK");
                }

                await PopulateViewBags();
                return PartialView("_CreateAppointmentPartial", appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating appointment");
                await PopulateViewBags();
                ModelState.AddModelError("", "An error occurred while creating the appointment.");
                return PartialView("_CreateAppointmentPartial", appointment);
            }
        }

        // GET: Assign Technician modal
        public async Task<IActionResult> AssignTechnicianModal(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            ViewBag.Appointment = appointment;

            // Use the same Position filter
            ViewBag.Technicians = await _context.Employees
                .Where(e => e.IsActive && !e.IsDeleted && e.Position == "Technician")
                .Select(e => new TechnicianViewModel
                {
                    EmployeeID = e.EmployeeID,
                    FullName = $"{e.FirstName} {e.LastName}"
                })
                .ToListAsync();

            return PartialView("_AssignTechnicianPartial");
        }

        // POST: Assign technician to customer appointment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTechnician(int id, int technicianId, DateTime? scheduledTime = null)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Customer)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (appointment == null)
                {
                    return NotFound();
                }

                var technician = await _context.Employees.FindAsync(technicianId);
                if (technician == null)
                {
                    return NotFound("Technician not found");
                }

                // Update appointment
                appointment.EmployeeID = technicianId;

                // Update time if provided
                if (scheduledTime.HasValue)
                {
                    appointment.StartTime = scheduledTime.Value;
                    appointment.EndTime = scheduledTime.Value.AddHours(1);
                }

                // Change status from Incomplete to Pending (ready for work)
                if (appointment.Status == "Incomplete")
                {
                    appointment.Status = "Pending";
                    appointment.Color = "#ffc107"; // Yellow for pending appointments
                }

                _context.Appointments.Update(appointment);
                await _context.SaveChangesAsync();

                // LOG TECHNICIAN ASSIGNMENT
                await _loggingService.LogActionAsync(
                    "TechnicianAssigned",
                    $"Technician assigned: {technician.FirstName} {technician.LastName} to {appointment.Customer?.Name} for {appointment.IssueType}",
                    User.Identity?.Name,
                    appointment.Customer?.Email,
                    appointment.Customer?.Name
                );

                return Content("OK");
            }
            catch (Exception ex)
            {
                // Return error message
                return Content($"Error: {ex.Message}");
            }
        }

        // GET: Edit modal - FIXED to properly pre-load data
        public async Task<IActionResult> EditModal(int id)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Employee)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (appointment == null)
                {
                    return NotFound($"Appointment with ID {id} not found");
                }

                await PopulateViewBags();
                return PartialView("_EditAppointmentPartial", appointment);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "EditModalError",
                    $"Error loading appointment ID {id} for edit: {ex.Message}",
                    User.Identity?.Name
                );
                return StatusCode(500, $"Error loading appointment: {ex.Message}");
            }
        }

        // POST: EditModal - FIXED for proper validation and error handling
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditModal(int id, Appointment appointment)
        {
            try
            {
                // First check if the appointment exists
                var existingAppointment = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (existingAppointment == null)
                {
                    return Json(new { success = false, error = "Appointment not found" });
                }

                // Validate the model
                if (ModelState.IsValid)
                {
                    // Update only editable fields
                    existingAppointment.CustomerID = appointment.CustomerID;
                    existingAppointment.EmployeeID = appointment.EmployeeID;
                    existingAppointment.StartTime = appointment.StartTime;
                    existingAppointment.EndTime = appointment.EndTime;
                    existingAppointment.IssueType = appointment.IssueType;
                    existingAppointment.OtherIssue = appointment.OtherIssue;
                    existingAppointment.Notes = appointment.Notes;
                    existingAppointment.TechnicianFaults = appointment.TechnicianFaults;
                    existingAppointment.Status = appointment.Status;
                    existingAppointment.Color = appointment.Color;

                    _context.Update(existingAppointment);
                    await _context.SaveChangesAsync();

                    // LOG APPOINTMENT UPDATE
                    var customer = await _context.Customers.FindAsync(appointment.CustomerID);
                    var employee = await _context.Employees.FindAsync(appointment.EmployeeID);

                    await _loggingService.LogActionAsync(
                        "AppointmentUpdated",
                        $"Appointment updated: {customer?.Name} with {employee?.FirstName} {employee?.LastName} on {appointment.StartTime:dd MMM yyyy}",
                        User.Identity?.Name,
                        customer?.Email,
                        customer?.Name
                    );

                    return Json(new { success = true, message = "Appointment updated successfully!" });
                }

                // If validation fails, return errors
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                await PopulateViewBags();
                return Json(new { success = false, error = "Validation failed", errors = errors });
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "EditModalError",
                    $"Error updating appointment ID {id}: {ex.Message}",
                    User.Identity?.Name
                );

                return Json(new { success = false, error = $"Error updating appointment: {ex.Message}" });
            }
        }
        // GET: View modal
        public async Task<IActionResult> ViewModal(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Include(a => a.Fridge) // Include the fridge
                    .ThenInclude(f => f.Product) // Include the product for the fridge
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            return PartialView("_ViewAppointmentPartial", appointment);
        }

        // GET: Delete modal
        public async Task<IActionResult> DeleteModal(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            return PartialView("_DeleteAppointmentPartial", appointment);
        }

        // POST: Delete modal
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteModalConfirmed(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment != null)
            {
                appointment.IsDeleted = true;
                _context.Update(appointment);
                await _context.SaveChangesAsync();
                
                // LOG APPOINTMENT DELETION
                await _loggingService.LogActionAsync(
                    "AppointmentDeleted",
                    $"Appointment deleted: {appointment.Customer?.Name} with {appointment.Employee?.FirstName} {appointment.Employee?.LastName} on {appointment.StartTime:dd MMM yyyy}",
                    User.Identity?.Name,
                    appointment.Customer?.Email,
                    appointment.Customer?.Name
                );
            }

            return Content("OK");
        }

        #endregion

        #region Past Appointments Edit Methods

        // GET: Edit Past Appointment - FIXED to use same approach as EditModal
        public async Task<IActionResult> EditPastAppointment(int id)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Employee)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (appointment == null)
                {
                    return NotFound($"Appointment with ID {id} not found");
                }

                await PopulateViewBags();
                return PartialView("_EditPastAppointmentPartial", appointment);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "EditPastAppointmentError",
                    $"Error loading past appointment ID {id}: {ex.Message}",
                    User.Identity?.Name
                );

                return StatusCode(500, new
                {
                    error = "Failed to load appointment",
                    message = ex.Message
                });
            }
        }

        // POST: Edit Past Appointment - FIXED to use JSON response
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPastAppointment(int id, Appointment appointment)
        {
            try
            {
                var existingAppointment = await _context.Appointments.FindAsync(id);
                if (existingAppointment == null)
                {
                    return Json(new { success = false, error = "Appointment not found" });
                }

                if (ModelState.IsValid)
                {
                    // Update past appointment details
                    existingAppointment.CustomerID = appointment.CustomerID;
                    existingAppointment.EmployeeID = appointment.EmployeeID;
                    existingAppointment.StartTime = appointment.StartTime;
                    existingAppointment.EndTime = appointment.EndTime;
                    existingAppointment.IssueType = appointment.IssueType;
                    existingAppointment.OtherIssue = appointment.OtherIssue;
                    existingAppointment.Notes = appointment.Notes;
                    existingAppointment.TechnicianFaults = appointment.TechnicianFaults;
                    existingAppointment.Status = appointment.Status;
                    existingAppointment.Color = appointment.Color;

                    _context.Update(existingAppointment);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "PastAppointmentUpdated",
                        $"Past appointment updated: ID {appointment.Id}",
                        User.Identity?.Name
                    );

                    return Json(new { success = true, message = "Past appointment updated successfully" });
                }

                // If validation fails, return errors
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Json(new { success = false, error = "Validation failed", errors = errors });
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "EditPastAppointmentError",
                    $"Error updating past appointment: {ex.Message}",
                    User.Identity?.Name
                );

                return Json(new { success = false, error = $"Error updating appointment: {ex.Message}" });
            }
        }

        #endregion

        #region Customer-Created Appointments Management

        // GET: Pending customer appointments
        public async Task<IActionResult> PendingAppointments(string search)
        {
            var query = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Where(a => !a.IsDeleted && (a.Status == "Pending" || a.Status == "Incomplete") && a.EndTime >= DateTime.Now);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => a.Customer.Name.Contains(search) ||
                                        a.IssueType.Contains(search) ||
                                        (a.Employee.FirstName + " " + a.Employee.LastName).Contains(search));
            }

            query = query.OrderBy(a => a.StartTime);

            ViewBag.Search = search;
            return View(await query.ToListAsync());
        }

        // POST: Mark appointment as completed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCompleted(int id, string technicianFaults = null)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            appointment.Status = "Completed";
            appointment.TechnicianFaults = technicianFaults;
            appointment.Color = "#28a745"; // Green for completed

            _context.Update(appointment);
            await _context.SaveChangesAsync();

            // LOG COMPLETION
            await _loggingService.LogActionAsync(
                "AppointmentCompleted",
                $"Appointment completed: {appointment.Customer?.Name} - {appointment.IssueType}",
                User.Identity?.Name,
                appointment.Customer?.Email,
                appointment.Customer?.Name
            );

            TempData["Success"] = "Appointment marked as completed!";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Details / Past Appointments

        [HttpGet]
        public async Task<IActionResult> PastAppointments(string search, string filter)
        {
            // Criteria for past appointments:
            // 1. IsDeleted = true OR
            // 2. EndTime < DateTime.Now (past appointments) OR  
            // 3. Status = "Completed" (completed appointments)
            var query = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Where(a => a.IsDeleted || a.EndTime < DateTime.Now || a.Status == "Completed");

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.Customer.Name.Contains(search) ||
                    a.Employee.FirstName.Contains(search) ||
                    a.Employee.LastName.Contains(search) ||
                    a.IssueType.Contains(search));
            }

            query = filter switch
            {
                "customer" => query.OrderBy(a => a.Customer.Name),
                "technician" => query.OrderBy(a => a.Employee.FirstName).ThenBy(a => a.Employee.LastName),
                "completed" => query.Where(a => a.Status == "Completed").OrderByDescending(a => a.EndTime),
                "deleted" => query.Where(a => a.IsDeleted).OrderByDescending(a => a.EndTime),
                "expired" => query.Where(a => a.EndTime < DateTime.Now && a.Status != "Completed" && !a.IsDeleted).OrderByDescending(a => a.EndTime),
                _ => query.OrderByDescending(a => a.EndTime)
            };

            ViewBag.Search = search;
            ViewBag.Filter = filter;

            return View(await query.ToListAsync());
        }

        #endregion

        #region JSON for FullCalendar + Drag&Drop

        [HttpGet]
        [Route("Dashboard/Appointments/GetAppointments")]
        public async Task<JsonResult> GetAppointments(int? technicianId = null)
        {
            try
            {
                Console.WriteLine($"GetAppointments called with technicianId: {technicianId}");

                var query = _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Employee)
                    .Where(a => !a.IsDeleted) // Only non-deleted appointments
                    .AsQueryable();

                // Filter by technician if specified
                if (technicianId.HasValue && technicianId.Value > 0)
                {
                    query = query.Where(a => a.EmployeeID == technicianId.Value);
                    Console.WriteLine($"Filtered by technician: {technicianId}");
                }

                var appointments = await query.ToListAsync();
                Console.WriteLine($"Found {appointments.Count} appointments");

                // Create events for FullCalendar with proper date formatting
                var events = appointments.Select(a => new
                {
                    id = a.Id,
                    title = $"{a.Customer?.Name ?? "No Customer"} - {a.IssueType ?? "No Issue"}",
                    start = a.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = a.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    color = GetEventColor(a.Status, a.Color),
                    extendedProps = new
                    {
                        customerName = a.Customer?.Name ?? "N/A",
                        technicianName = a.Employee != null ? $"{a.Employee.FirstName} {a.Employee.LastName}" : "Not Assigned",
                        issueType = a.IssueType ?? "N/A",
                        notes = a.Notes ?? "N/A",
                        status = a.Status ?? "N/A"
                    }
                }).ToList();

                Console.WriteLine($"Returning {events.Count} events to calendar");

                // Debug: log first few events
                foreach (var evt in events.Take(3))
                {
                    Console.WriteLine($"Event: {evt.title} | Start: {evt.start} | End: {evt.end}");
                }

                return Json(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAppointments method");
                Console.WriteLine($"Error in GetAppointments: {ex.Message}");
                return Json(new List<object>());
            }
        }

        // Helper method to determine event color
        private string GetEventColor(string status, string customColor)
        {
            if (!string.IsNullOrEmpty(customColor))
                return customColor;

            return status?.ToLower() switch
            {
                "completed" => "#28a745",    // Green
                "pending" => "#ffc107",      // Yellow
                "incomplete" => "#dc3545",   // Red
                "cancelled" => "#6c757d",    // Gray for cancelled
                "rescheduled" => "#17a2b8",  // Teal
                "scheduled" => "#007bff",    // Blue
                _ => "#6c757d"               // Gray
            };
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDate([FromBody] UpdateAppointmentTimeRequest request)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.Id == request.Id);

                if (appointment == null)
                {
                    return NotFound(new { error = "Appointment not found" });
                }

                var oldStart = appointment.StartTime;
                var oldEnd = appointment.EndTime;

                appointment.StartTime = request.Start;
                appointment.EndTime = request.End;

                // Validate the new times
                if (appointment.EndTime <= appointment.StartTime)
                {
                    return BadRequest(new { error = "End time must be after start time" });
                }

                _context.Update(appointment);
                await _context.SaveChangesAsync();

                // LOG APPOINTMENT RESCHEDULE
                await _loggingService.LogActionAsync(
                    "AppointmentRescheduled",
                    $"Appointment rescheduled: {appointment.Customer?.Name} from {oldStart:dd MMM yyyy HH:mm} to {request.Start:dd MMM yyyy HH:mm}",
                    User.Identity?.Name,
                    appointment.Customer?.Email,
                    appointment.Customer?.Name
                );

                return Ok(new { message = "Appointment updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating appointment date");
                return StatusCode(500, new { error = "An error occurred while updating the appointment" });
            }
        }

        // Add this class for the request model
        public class UpdateAppointmentTimeRequest
        {
            public int Id { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }

        #endregion

        #region Export PDF / Excel

        [HttpGet]
        public async Task<IActionResult> ExportPdf()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Where(a => !a.IsDeleted && a.EndTime >= DateTime.Now)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            // LOG PDF EXPORT
            await _loggingService.LogActionAsync(
                "AppointmentsPDFExported",
                "Appointments PDF report exported",
                User.Identity?.Name
            );

            return File(GeneratePdf(appointments, "Appointments Report"), "application/pdf", "Appointments.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .ToListAsync();

            // LOG EXCEL EXPORT
            await _loggingService.LogActionAsync(
                "AppointmentsExcelExported",
                "Appointments Excel report exported",
                User.Identity?.Name
            );

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Appointments");

            // Header row
            ws.Cell(1, 1).Value = "Customer";
            ws.Cell(1, 2).Value = "Technician";
            ws.Cell(1, 3).Value = "Start Time";
            ws.Cell(1, 4).Value = "End Time";
            ws.Cell(1, 5).Value = "Issue Type";
            ws.Cell(1, 6).Value = "Notes";
            ws.Cell(1, 7).Value = "Status";
            ws.Cell(1, 8).Value = "Technician Faults";
            ws.Cell(1, 9).Value = "Color";

            // Style the header
            var headerRange = ws.Range(1, 1, 1, 9);
            headerRange.Style.Fill.BackgroundColor = XLColor.Blue;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Data rows
            int row = 2;
            foreach (var a in appointments)
            {
                ws.Cell(row, 1).Value = a.Customer?.Name ?? "-";
                ws.Cell(row, 2).Value = $"{a.Employee?.FirstName} {a.Employee?.LastName}" ?? "-";
                ws.Cell(row, 3).Value = a.StartTime;
                ws.Cell(row, 4).Value = a.EndTime;
                ws.Cell(row, 5).Value = a.IssueType ?? "-";
                ws.Cell(row, 6).Value = a.Notes ?? "-";
                ws.Cell(row, 7).Value = a.Status ?? "-";
                ws.Cell(row, 8).Value = a.TechnicianFaults ?? "-";
                ws.Cell(row, 9).Value = a.Color ?? "-";
                row++;
            }

            // Auto-fit columns
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Appointments.xlsx");
        }

        #endregion

        #region Helpers

        private void ValidateBookingRules(Appointment appointment)
        {
            // For customer-created appointments (Incomplete status, no technician assigned), be more lenient
            var isCustomerCreated = appointment.Status == "Incomplete" && appointment.EmployeeID == null;

            if (!isCustomerCreated)
            {
                // Only apply strict rules for admin-managed appointments
                if (appointment.StartTime.Date <= DateTime.Today)
                    ModelState.AddModelError("StartTime", "Appointments must be booked from the next day onwards.");
                if (appointment.StartTime.DayOfWeek == DayOfWeek.Sunday)
                    ModelState.AddModelError("StartTime", "Appointments cannot be booked on Sundays.");
                if (appointment.StartTime.Hour < 8 || appointment.StartTime.Hour > 16)
                    ModelState.AddModelError("StartTime", "Appointments must start between 08:00 and 16:00.");
            }

            if (appointment.EndTime == default || appointment.EndTime <= appointment.StartTime)
            {
                appointment.EndTime = appointment.StartTime.AddHours(1);
                if (appointment.EndTime.Hour > 17)
                    appointment.EndTime = new DateTime(appointment.StartTime.Year, appointment.StartTime.Month, appointment.StartTime.Day, 17, 0, 0);
            }
        }

        private byte[] GeneratePdf(List<Appointment> appointments, string reportTitle)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // Header
                    page.Header().Column(headerColumn =>
                    {
                        headerColumn.Spacing(10);

                        // Company Logo/Name and Report Info Row
                        headerColumn.Item().Row(headerRow =>
                        {
                            // Left side - Company Info
                            headerRow.RelativeItem().Column(companyColumn =>
                            {
                                companyColumn.Item().Text("Fridge Frenzy")
                                    .FontSize(28)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken4);

                                companyColumn.Item().PaddingTop(5).Text("Premium Refrigeration Services")
                                    .FontSize(13)
                                    .SemiBold()
                                    .FontColor(Colors.Blue.Darken2);

                                companyColumn.Item().PaddingTop(10).Text("123 Main Street")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);

                                companyColumn.Item().Text("Port Elizabeth, Eastern Cape, 6001")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);

                                companyColumn.Item().PaddingTop(3).Text("South Africa")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);

                                companyColumn.Item().PaddingTop(5).Text("Email: info@fridgefrenzy.com")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);

                                companyColumn.Item().Text("Phone: 081 028 6437")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);
                            });

                            // Right side - Report Info
                            headerRow.ConstantItem(180).Column(reportColumn =>
                            {
                                reportColumn.Item().Background(Colors.Blue.Darken4)
                                    .Padding(10)
                                    .Column(repColumn =>
                                    {
                                        repColumn.Item().AlignCenter().Text("APPOINTMENTS")
                                            .FontSize(16)
                                            .Bold()
                                            .FontColor(Colors.White);

                                        repColumn.Item().PaddingTop(5).AlignCenter().Text("REPORT")
                                            .FontSize(16)
                                            .Bold()
                                            .FontColor(Colors.White);
                                    });

                                reportColumn.Item().PaddingTop(10).AlignRight().Text($"Generated: {DateTime.Now:dd MMMM yyyy}")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);

                                reportColumn.Item().AlignRight().Text($"Time: {DateTime.Now:HH:mm}")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);

                                reportColumn.Item().PaddingTop(5).AlignRight().Text($"Total Records: {appointments.Count}")
                                    .FontSize(9)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken4);
                            });
                        });

                        headerColumn.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
                    });

                    // Content
                    page.Content().PaddingTop(20).Column(contentColumn =>
                    {
                        contentColumn.Spacing(15);

                        // Report Title
                        contentColumn.Item().Background(Colors.Grey.Lighten3)
                            .Padding(15)
                            .Column(titleColumn =>
                            {
                                titleColumn.Item().Text(reportTitle.ToUpper())
                                    .FontSize(14)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken4);

                                titleColumn.Item().PaddingTop(5).Text($"Appointment Summary - {DateTime.Now:dd MMMM yyyy}")
                                    .FontSize(11)
                                    .FontColor(Colors.Grey.Darken2);
                            });

                        // Appointments Table
                        contentColumn.Item().Table(table =>
                        {
                            // Define columns with better proportions for appointment data
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);  // Customer
                                columns.RelativeColumn(1.2f);  // Technician
                                columns.RelativeColumn(1.2f);  // Start
                                columns.RelativeColumn(1.2f);  // End
                                columns.RelativeColumn(1.5f);  // Issue
                                columns.RelativeColumn(2f);    // Notes
                                columns.ConstantColumn(70);    // Status
                                columns.RelativeColumn(1.2f);  // Faults
                            });

                            // Table Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("CUSTOMER").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("TECHNICIAN").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("START TIME").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("END TIME").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("ISSUE TYPE").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("NOTES").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("STATUS").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("FAULTS").FontColor(Colors.White).Bold().FontSize(9);
                            });

                            // Table Rows
                            bool alternate = false;
                            foreach (var appointment in appointments)
                            {
                                var bgColor = alternate ? Colors.Grey.Lighten4 : Colors.White;
                                alternate = !alternate;

                                // Status color coding
                                var statusColor = appointment.Status?.ToLower() switch
                                {
                                    "completed" => Colors.Green.Darken2,
                                    "in progress" => Colors.Orange.Darken2,
                                    "scheduled" => Colors.Blue.Darken2,
                                    "cancelled" => Colors.Red.Darken2,
                                    _ => Colors.Grey.Darken2
                                };

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(appointment.Customer?.Name ?? "-")
                                    .FontSize(9)
                                    .FontColor(Colors.Black);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text($"{appointment.Employee?.FirstName} {appointment.Employee?.LastName}" ?? "-")
                                    .FontSize(9)
                                    .FontColor(Colors.Black);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(appointment.StartTime.ToString("g"))
                                    .FontSize(9)
                                    .FontColor(Colors.Black);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(appointment.EndTime.ToString("g"))
                                    .FontSize(9)
                                    .FontColor(Colors.Black);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(appointment.IssueType ?? "-")
                                    .FontSize(9)
                                    .FontColor(Colors.Black);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(appointment.Notes ?? "-")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(appointment.Status ?? "-")
                                    .FontSize(9)
                                    .Bold()
                                    .FontColor(statusColor);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(appointment.TechnicianFaults ?? "-")
                                    .FontSize(9)
                                    .FontColor(Colors.Black);
                            }
                        });

                        // Summary Section
                        if (appointments.Any())
                        {
                            contentColumn.Item().PaddingTop(20).Row(summaryRow =>
                            {
                                summaryRow.RelativeItem().Background(Colors.Blue.Lighten4)
                                    .Padding(15)
                                    .Column(summaryColumn =>
                                    {
                                        var completed = appointments.Count(a => a.Status?.ToLower() == "completed");
                                        var inProgress = appointments.Count(a => a.Status?.ToLower() == "in progress");
                                        var scheduled = appointments.Count(a => a.Status?.ToLower() == "scheduled");

                                        summaryColumn.Item().Text("APPOINTMENT SUMMARY")
                                            .FontSize(11)
                                            .Bold()
                                            .FontColor(Colors.Blue.Darken4);

                                        summaryColumn.Item().PaddingTop(8).Text($"Total Appointments: {appointments.Count}")
                                            .FontSize(10)
                                            .FontColor(Colors.Grey.Darken2);

                                        summaryColumn.Item().Text($"Completed: {completed} | In Progress: {inProgress} | Scheduled: {scheduled}")
                                            .FontSize(10)
                                            .FontColor(Colors.Grey.Darken2);

                                        var todayAppointments = appointments.Count(a => a.StartTime.Date == DateTime.Today);
                                        summaryColumn.Item().Text($"Today's Appointments: {todayAppointments}")
                                            .FontSize(10)
                                            .FontColor(Colors.Grey.Darken2);
                                    });
                            });
                        }
                    });

                    // Footer
                    page.Footer().Column(footerColumn =>
                    {
                        footerColumn.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                        footerColumn.Item().PaddingTop(15).AlignCenter()
                            .Text("Fridge Frenzy - Professional Refrigeration Services")
                            .FontSize(11)
                            .SemiBold()
                            .FontColor(Colors.Blue.Darken3);

                        footerColumn.Item().PaddingTop(5).AlignCenter()
                            .Text("For any inquiries, please contact us at info@fridgefrenzy.com or call 081 028 6437")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);

                        footerColumn.Item().PaddingTop(8).AlignCenter()
                            .Text("© 2025 Fridge Frenzy - Premium Refrigeration Services - All Rights Reserved")
                            .FontSize(8)
                            .Italic()
                            .FontColor(Colors.Grey.Darken1);

                        footerColumn.Item().PaddingTop(10).AlignCenter().Text(text =>
                        {
                            text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken2);
                            text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    });
                });
            }).GeneratePdf();
        }

        private byte[] GenerateCustomerReportPdf(List<Customer> customers, string reportTitle)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // Header
                    page.Header().Column(headerColumn =>
                    {
                        headerColumn.Spacing(10);

                        headerColumn.Item().Row(headerRow =>
                        {
                            // Left side - Company Info
                            headerRow.RelativeItem().Column(companyColumn =>
                            {
                                companyColumn.Item().Text("Fridge Frenzy")
                                    .FontSize(28)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken4);

                                companyColumn.Item().PaddingTop(5).Text("Premium Refrigeration Services")
                                    .FontSize(13)
                                    .SemiBold()
                                    .FontColor(Colors.Blue.Darken2);

                                companyColumn.Item().PaddingTop(10).Text("123 Main Street")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);

                                companyColumn.Item().Text("Port Elizabeth, Eastern Cape, 6001")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);
                            });

                            // Right side - Report Info
                            headerRow.ConstantItem(180).Column(reportColumn =>
                            {
                                reportColumn.Item().Background(Colors.Blue.Darken4)
                                    .Padding(10)
                                    .Column(repColumn =>
                                    {
                                        repColumn.Item().AlignCenter().Text("CUSTOMER")
                                            .FontSize(16)
                                            .Bold()
                                            .FontColor(Colors.White);

                                        repColumn.Item().PaddingTop(5).AlignCenter().Text("REPORT")
                                            .FontSize(16)
                                            .Bold()
                                            .FontColor(Colors.White);
                                    });

                                reportColumn.Item().PaddingTop(10).AlignRight().Text($"Generated: {DateTime.Now:dd MMMM yyyy}")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken2);

                                reportColumn.Item().AlignRight().Text($"Total Customers: {customers.Count}")
                                    .FontSize(9)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken4);
                            });
                        });

                        headerColumn.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
                    });

                    // Content
                    page.Content().PaddingTop(20).Column(contentColumn =>
                    {
                        contentColumn.Spacing(15);

                        // Report Title
                        contentColumn.Item().Background(Colors.Grey.Lighten3)
                            .Padding(15)
                            .Column(titleColumn =>
                            {
                                titleColumn.Item().Text(reportTitle.ToUpper())
                                    .FontSize(14)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken4);

                                titleColumn.Item().PaddingTop(5).Text($"Customer Database - {DateTime.Now:dd MMMM yyyy}")
                                    .FontSize(11)
                                    .FontColor(Colors.Grey.Darken2);
                            });

                        // Customers Table
                        contentColumn.Item().Table(table =>
                        {
                            // Define columns for customer data
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);  // Name
                                columns.RelativeColumn(1.2f);  // Email
                                columns.RelativeColumn(1f);    // Phone
                                columns.RelativeColumn(2f);    // Address
                                columns.ConstantColumn(80);    // Since
                                columns.ConstantColumn(70);    // Status
                            });

                            // Table Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("CUSTOMER NAME").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("EMAIL").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("PHONE").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("ADDRESS").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("MEMBER SINCE").FontColor(Colors.White).Bold().FontSize(9);

                                header.Cell().Background(Colors.Blue.Darken4).Padding(8)
                                    .Text("STATUS").FontColor(Colors.White).Bold().FontSize(9);
                            });

                            // Table Rows
                            bool alternate = false;
                            foreach (var customer in customers)
                            {
                                var bgColor = alternate ? Colors.Grey.Lighten4 : Colors.White;
                                alternate = !alternate;

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(customer.Name ?? "-")
                                    .FontSize(9)
                                    .SemiBold()
                                    .FontColor(Colors.Black);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(customer.Email ?? "-")
                                    .FontSize(9)
                                    .FontColor(Colors.Blue.Darken2);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text(customer.ContactNumber ?? "-")
                                    .FontSize(9)
                                    .FontColor(Colors.Black);

                                table.Cell()
                                    .Background(bgColor)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(8)
                                    .Text("Active")
                                    .FontSize(9)
                                    .Bold()
                                    .FontColor(Colors.Green.Darken2);
                            }
                        });

                        // Summary Section
                        contentColumn.Item().PaddingTop(20).Background(Colors.Blue.Lighten4)
                            .Padding(15)
                            .Column(summaryColumn =>
                            {
                                summaryColumn.Item().Text("CUSTOMER SUMMARY")
                                    .FontSize(11)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken4);

                                summaryColumn.Item().PaddingTop(8).Text($"Total Customers: {customers.Count}")
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken2);

                                
                            });
                    });

                    // Footer
                    page.Footer().Column(footerColumn =>
                    {
                        footerColumn.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                        footerColumn.Item().PaddingTop(15).AlignCenter()
                            .Text("Confidential Customer Report - Fridge Frenzy")
                            .FontSize(11)
                            .SemiBold()
                            .FontColor(Colors.Blue.Darken3);

                        footerColumn.Item().PaddingTop(5).AlignCenter()
                            .Text("For any inquiries, please contact us at info@fridgefrenzy.com or call 081 028 6437")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);

                        footerColumn.Item().PaddingTop(10).AlignCenter().Text(text =>
                        {
                            text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken2);
                            text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    });
                });
            }).GeneratePdf();
        }

        #endregion
    }
}