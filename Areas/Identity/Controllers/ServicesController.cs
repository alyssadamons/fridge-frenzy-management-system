using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Areas.Identity.ViewModels;
using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Commerce.Areas.Identity.Controllers
{
    [Area("Identity")]
    [Authorize]
    public class ServicesController : Controller
    {
       
        private readonly ApplicationDbContext _applicationContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ServicesController> _logger;

        public ServicesController(
            
            ApplicationDbContext applicationContext,
            UserManager<ApplicationUser> userManager,
            ILogger<ServicesController> logger)
        {
            
            _applicationContext = applicationContext;
            _userManager = userManager;
            _logger = logger;
        }

        // ======================
        // SERVICES MAIN PAGE
        // ======================
        public IActionResult Index()
        {
            return View();
        }

        // ======================
        // MY FRIDGES MANAGEMENT
        // ======================
        [HttpGet]
        public async Task<IActionResult> MyFridges()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["Error"] = "Customer profile not found. Please complete your profile.";
                    return View(new CustomerFridgesViewModel());
                }

                var fridges = await _applicationContext.FridgeRegistrations
                    .Include(f => f.Product)
                    .Include(f => f.OrderItem)
                    .Where(f => f.CustomerId == customer.CustomerID && f.IsActive)
                    .OrderByDescending(f => f.RegistrationDate)
                    .ToListAsync();

                var serviceHistory = new Dictionary<int, List<Appointment>>();
                foreach (var fridge in fridges)
                {
                    var appointments = await _applicationContext.Appointments
                        .Include(a => a.Employee)
                        .Where(a => a.FridgeId == fridge.Id && !a.IsDeleted)
                        .OrderByDescending(a => a.StartTime)
                        .ToListAsync();
                    serviceHistory[fridge.Id] = appointments;
                }

                var model = new CustomerFridgesViewModel
                {
                    Fridges = fridges,
                    FridgeServiceHistory = serviceHistory,
                    TotalFridges = fridges.Count,
                    TotalServices = serviceHistory.Sum(sh => sh.Value.Count)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fridges for user");
                TempData["Error"] = "Error loading your fridges. Please try again.";
                return View(new CustomerFridgesViewModel());
            }
        }

        // ======================
        // FRIDGE SERVICE HISTORY
        // ======================
        [HttpGet]
        public async Task<IActionResult> FridgeServiceHistory(int fridgeId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("MyFridges");
            }

            // Verify the fridge belongs to the customer
            var fridge = await _applicationContext.FridgeRegistrations
                .FirstOrDefaultAsync(f => f.Id == fridgeId && f.CustomerId == customer.CustomerID && f.IsActive);

            if (fridge == null)
            {
                TempData["Error"] = "Fridge not found or access denied.";
                return RedirectToAction("MyFridges");
            }

            var appointments = await _applicationContext.Appointments
                .Include(a => a.Employee)
                .Where(a => a.FridgeId == fridgeId && !a.IsDeleted)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            ViewBag.Fridge = fridge;
            return View(appointments);
        }

        // ======================
        // REGISTER FRIDGE FROM ORDER - FIXED VERSION
        // ======================
        [HttpGet]
        public async Task<IActionResult> RegisterFridgeFromOrder(int orderId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("MyFridges");
            }

            var order = await _applicationContext.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerID == customer.CustomerID);

            if (order == null)
            {
                TempData["Error"] = "Order not found or access denied.";
                return RedirectToAction("MyFridges");
            }

            // Get ALL registered fridges for this customer (not just from this order)
            var registeredFridges = await _applicationContext.FridgeRegistrations
                .Where(f => f.CustomerId == customer.CustomerID && f.IsActive)
                .ToListAsync();

            _logger.LogInformation("Found {RegisteredCount} registered fridges for customer {CustomerId}",
                registeredFridges.Count, customer.CustomerID);

            var model = new FridgeRegistrationViewModel
            {
                OrderId = orderId
            };

            ViewBag.Order = order;
            ViewBag.OrderItems = order.OrderItems.ToList();
            ViewBag.RegisteredFridges = registeredFridges;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterFridgeFromOrder(
    int OrderId,
    int ProductId,
    int OrderItemId,
    string FridgeName,
    string Brand,
    DateTime PurchaseDate)
        {
            try
            {
                _logger.LogInformation("=== RegisterFridgeFromOrder POST called ===");
                _logger.LogInformation("Parameters - OrderId: {OrderId}, ProductId: {ProductId}, FridgeName: {FridgeName}",
                    OrderId, ProductId, FridgeName);

                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found for current user");
                    TempData["Error"] = "Customer profile not found. Please complete your profile.";
                    return RedirectToAction("RegisterFridgeFromOrder", new { orderId = OrderId });
                }

                // Check if already registered - MORE SPECIFIC CHECK
                var existingFridge = await _applicationContext.FridgeRegistrations
                    .FirstOrDefaultAsync(f => f.CustomerId == customer.CustomerID &&
                                            f.OrderId == OrderId &&
                                            f.ProductId == ProductId &&
                                            f.IsActive);

                if (existingFridge != null)
                {
                    _logger.LogWarning("Fridge already registered - OrderId: {OrderId}, ProductId: {ProductId}", OrderId, ProductId);
                    TempData["Warning"] = $"This fridge '{existingFridge.FridgeName}' is already registered!";
                    return RedirectToAction("RegisterFridgeFromOrder", new { orderId = OrderId });
                }

                // Create fridge registration
                var fridge = new FridgeRegistration
                {
                    OrderId = OrderId,
                    ProductId = ProductId,
                    CustomerId = customer.CustomerID,
                    Nickname = FridgeName ?? "My Fridge",
                    FridgeName = FridgeName ?? "",
                    Brand = Brand ?? "Fridge Frenzy",
                    PurchaseDate = PurchaseDate,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    RegistrationDate = DateTime.Now
                };

                _logger.LogInformation("Creating fridge: OrderId={OrderId}, ProductId={ProductId}, CustomerId={CustomerId}",
                    fridge.OrderId, fridge.ProductId, fridge.CustomerId);

                _applicationContext.FridgeRegistrations.Add(fridge);
                await _applicationContext.SaveChangesAsync();

                _logger.LogInformation("✅ Fridge registered successfully - ID: {FridgeId}", fridge.Id);
                TempData["Success"] = $"Fridge '{fridge.Nickname}' registered successfully! You can now book maintenance appointments for it.";
                return RedirectToAction("MyFridges");
            }
            catch (DbUpdateException dbEx)
            {
                var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
                _logger.LogError(dbEx, "Database error registering fridge: {Error}", innerMsg);
                TempData["Error"] = $"Database error: {innerMsg}";
                return RedirectToAction("RegisterFridgeFromOrder", new { orderId = OrderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error registering fridge: {Message}", ex.Message);
                TempData["Error"] = $"An unexpected error occurred: {ex.Message}";
                return RedirectToAction("RegisterFridgeFromOrder", new { orderId = OrderId });
            }
        }

        // ======================
        // BOOK APPOINTMENT - GET METHOD
        // ======================
        [HttpGet]
        public async Task<IActionResult> BookAppointment()
        {
            try
            {
                _logger.LogInformation("BookAppointment GET method called");

                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found for user");
                    TempData["Error"] = "Customer profile not found. Please complete your profile first.";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation("Found customer: {CustomerId}", customer.CustomerID);

                // Get customer fridges
                var customerFridges = await _applicationContext.FridgeRegistrations
                    .Where(f => f.CustomerId == customer.CustomerID && f.IsActive)
                    .OrderBy(f => f.FridgeName)
                    .ToListAsync();

                _logger.LogInformation("Found {FridgeCount} fridges for customer", customerFridges.Count);

                var model = new AppointmentWithFridgeViewModel
                {
                    PreferredDateTime = GetNextAvailableDateTime(),
                    CustomerFridges = customerFridges
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading book appointment page");
                TempData["Error"] = "Error loading appointment booking page. Please try again.";
                return RedirectToAction("Index");
            }
        }

        // ======================
        // BOOK APPOINTMENT WITH FRIDGE SELECTION - FIXED
        // ======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(AppointmentWithFridgeViewModel model)
        {
            try
            {
                _logger.LogInformation("BookAppointment POST method called");
                _logger.LogInformation("Model received - OtherFridgeBrand: {Brand}, OtherFridgeModel: {Model}",
                    model.OtherFridgeBrand, model.OtherFridgeModel);

                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer profile not found. Please complete your profile." });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                    return Json(new { success = false, message = "Please fix the validation errors.", errors = errors });
                }

                // Validate appointment date/time
                var validationErrors = ValidateAppointmentDateTime(model.PreferredDateTime);
                if (validationErrors.Any())
                {
                    _logger.LogWarning("Business validation failed: {Errors}", string.Join(", ", validationErrors));
                    return Json(new { success = false, message = validationErrors.First() });
                }

                // Handle fridge selection
                int? fridgeId = null;

                if (model.SelectedFridgeId.HasValue && model.SelectedFridgeId > 0)
                {
                    // Verify the selected fridge belongs to the customer
                    var fridge = await _applicationContext.FridgeRegistrations
                        .FirstOrDefaultAsync(f => f.Id == model.SelectedFridgeId && f.CustomerId == customer.CustomerID && f.IsActive);

                    if (fridge != null)
                    {
                        fridgeId = model.SelectedFridgeId.Value;
                        _logger.LogInformation("Selected existing fridge: {FridgeId}", fridgeId);
                    }
                    else
                    {
                        _logger.LogWarning("Selected fridge {FridgeId} not found or doesn't belong to customer", model.SelectedFridgeId);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(model.OtherFridgeModel))
                {
                    // Validate brand
                    if (string.IsNullOrWhiteSpace(model.OtherFridgeBrand))
                    {
                        return Json(new { success = false, message = "Please select a brand for the fridge." });
                    }

                    try
                    {
                        // For "Other" fridges, we need an OrderId and ProductId
                        // Since these are temporary fridges NOT from orders, we have options:
                        // Option 1: Create a dummy order/product (not recommended)
                        // Option 2: Change database to make OrderId and ProductId nullable
                        // Option 3: Don't allow "Other" fridges to be registered permanently

                        // ⚠️ PROBLEM: Your database requires OrderId and ProductId
                        // We CANNOT create a temporary fridge without these!

                        _logger.LogWarning("Cannot create temporary fridge - OrderId and ProductId are required in database");

                        return Json(new
                        {
                            success = false,
                            message = "Please select a registered fridge or register your fridge first from your order history."
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error with Other fridge");
                        return Json(new { success = false, message = $"Error: {ex.Message}" });
                    }
                }
                else
                {
                    _logger.LogWarning("No fridge selected for appointment");
                    // Allow appointments without fridge selection
                }

                // Handle issue type
                string issueType = model.IssueType;
                if (model.IssueType == "Other" && !string.IsNullOrWhiteSpace(model.OtherIssue))
                {
                    issueType = model.OtherIssue.Trim();
                    _logger.LogInformation("Using custom issue type: {IssueType}", issueType);
                }

                // Get or create default employee
                int defaultEmployeeId;
                try
                {
                    defaultEmployeeId = await GetOrCreateDefaultEmployeeAsync();
                    _logger.LogInformation("Got default employee ID: {EmployeeId}", defaultEmployeeId);
                }
                catch (Exception empEx)
                {
                    _logger.LogError(empEx, "Failed to get default employee");
                    return Json(new { success = false, message = "System error: Unable to assign technician. Please contact support." });
                }

                // Create appointment
                var appointment = new Appointment
                {
                    CustomerID = customer.CustomerID,
                    EmployeeID = defaultEmployeeId,
                    StartTime = model.PreferredDateTime,
                    EndTime = model.PreferredDateTime.AddHours(1),
                    IssueType = issueType ?? "General Service",  // Ensure never null
                    OtherIssue = model.OtherIssue?.Trim(),
                    Notes = model.Notes?.Trim(),
                    Status = "Incomplete",      // Required with default
                    Color = "#dc3545",          // Red for incomplete
                    IsDeleted = false,          // Explicitly set (required in DB)
                    FridgeId = fridgeId         // Nullable, can be null
                };

                _logger.LogInformation("Creating appointment: CustID={Cust}, EmpID={Emp}, Issue={Issue}, " +
                    "Status={Status}, IsDeleted={Deleted}, FridgeId={Fridge}",
                    appointment.CustomerID, appointment.EmployeeID, appointment.IssueType,
                    appointment.Status, appointment.IsDeleted, appointment.FridgeId);

                try
                {
                    _applicationContext.Appointments.Add(appointment);
                    await _applicationContext.SaveChangesAsync();

                    _logger.LogInformation("✅ Appointment created successfully: ID={Id}", appointment.Id);

                    return Json(new
                    {
                        success = true,
                        message = "Appointment booked successfully! We'll assign a technician and contact you soon.",
                        redirectUrl = Url.Action("MyAppointments", "Services", new { area = "Identity" })
                    });
                }
                catch (DbUpdateException dbEx)
                {
                    var innermost = dbEx.InnerException ?? dbEx;
                    var errorMsg = innermost.Message;

                    _logger.LogError(dbEx, "Database error creating appointment: {Error}", errorMsg);

                    return Json(new { success = false, message = $"Database error: {errorMsg}" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error creating appointment");
                    return Json(new { success = false, message = $"Error: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating appointment. Message: {Message}, Inner: {InnerMessage}, StackTrace: {StackTrace}",
                    ex.Message,
                    ex.InnerException?.Message ?? "none",
                    ex.StackTrace);

                return Json(new
                {
                    success = false,
                    message = $"An error occurred: {ex.Message}. Please contact support if the problem persists."
                });
            }
        }

        private async Task<int?> HandleFridgeSelection(AppointmentWithFridgeViewModel model, int customerId)
        {
            if (model.SelectedFridgeId.HasValue && model.SelectedFridgeId > 0)
            {
                // Verify existing fridge belongs to customer
                var fridge = await _applicationContext.FridgeRegistrations
                    .FirstOrDefaultAsync(f => f.Id == model.SelectedFridgeId && f.CustomerId == customerId && f.IsActive);
                return fridge?.Id;
            }
            else if (!string.IsNullOrEmpty(model.OtherFridgeModel))
            {
                // Create temporary fridge registration with brand
                var tempFridge = new FridgeRegistration
                {
                    CustomerId = customerId,
                    FridgeName = model.OtherFridgeModel,

                    IsActive = true,
                    RegistrationDate = DateTime.Now,
                    PurchaseDate = DateTime.Now // Default purchase date
                };

                _applicationContext.FridgeRegistrations.Add(tempFridge);
                await _applicationContext.SaveChangesAsync();
                return tempFridge.Id;
            }

            return null;
        }

        // ======================
        // GET APPOINTMENT DETAILS FOR MODAL
        // ======================
        [HttpGet]
        public async Task<IActionResult> GetAppointmentDetails(int id)
        {
            var customer = await GetCurrentCustomerAsync();

            var appointment = await _applicationContext.Appointments
                .Include(a => a.Employee)
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Id == id && a.CustomerID == customer.CustomerID && !a.IsDeleted);

            if (appointment == null)
            {
                return Content("<div class='modal-header'><h5 class='modal-title'>Appointment Not Found</h5></div><div class='modal-body'><p>Appointment not found or access denied.</p></div>");
            }

            // Manually load fridge from ApplicationDbContext if needed
            if (appointment.FridgeId.HasValue)
            {
                appointment.Fridge = await _applicationContext.FridgeRegistrations
                    .FirstOrDefaultAsync(f => f.Id == appointment.FridgeId.Value);
            }

            return PartialView("_AppointmentDetailsPartial", appointment);
        }

        // ======================
        // EDIT APPOINTMENT WITH FRIDGE SELECTION
        // ======================
        [HttpGet]
        public async Task<IActionResult> EditAppointment(int id)
        {
            var customer = await GetCurrentCustomerAsync();

            var appointment = await _applicationContext.Appointments
                .FirstOrDefaultAsync(a => a.Id == id && a.CustomerID == customer.CustomerID && !a.IsDeleted);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment not found.";
                return RedirectToAction("MyAppointments");
            }

            // Allow edit even if technician assigned (as long as not completed)
            if (appointment.Status == "Completed")
            {
                TempData["Error"] = "Cannot edit a completed appointment.";
                return RedirectToAction("MyAppointments");
            }

            // Manually load fridge from ApplicationDbContext if needed
            if (appointment.FridgeId.HasValue)
            {
                appointment.Fridge = await _applicationContext.FridgeRegistrations
                    .FirstOrDefaultAsync(f => f.Id == appointment.FridgeId.Value);
            }

            var customerFridges = await _applicationContext.FridgeRegistrations
                .Where(f => f.CustomerId == customer.CustomerID && f.IsActive)
                .OrderBy(f => f.FridgeName)
                .ToListAsync();

            var model = new AppointmentWithFridgeViewModel
            {
                IssueType = appointment.IssueType,
                PreferredDateTime = appointment.StartTime,
                Notes = appointment.Notes,
                CustomerFridges = customerFridges,
                SelectedFridgeId = appointment.FridgeId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAppointment(int id, AppointmentWithFridgeViewModel model)
        {
            try
            {
                _logger.LogInformation("EditAppointment POST called for ID: {Id}", id);

                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found for current user");
                    return Json(new { success = false, message = "Customer profile not found. Please complete your profile." });
                }

                var appointment = await _applicationContext.Appointments
                    .FirstOrDefaultAsync(a => a.Id == id && a.CustomerID == customer.CustomerID && !a.IsDeleted);

                if (appointment == null)
                {
                    _logger.LogWarning("Appointment {Id} not found for customer {CustomerId}", id, customer.CustomerID);
                    return Json(new { success = false, message = "Appointment not found." });
                }

                // Allow edit even if technician assigned (as long as not completed)
                if (appointment.Status == "Completed")
                {
                    return Json(new { success = false, message = "Cannot edit a completed appointment." });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                    return Json(new { success = false, message = "Please fix the validation errors.", errors = errors });
                }

                // Validate appointment date/time
                var validationErrors = ValidateAppointmentDateTime(model.PreferredDateTime);
                if (validationErrors.Any())
                {
                    _logger.LogWarning("Business validation failed: {Errors}", string.Join(", ", validationErrors));
                    return Json(new { success = false, message = validationErrors.First() });
                }

                // Handle fridge selection
                int? fridgeId = null;
                if (model.SelectedFridgeId.HasValue && model.SelectedFridgeId > 0)
                {
                    var fridge = await _applicationContext.FridgeRegistrations
                        .FirstOrDefaultAsync(f => f.Id == model.SelectedFridgeId && f.CustomerId == customer.CustomerID && f.IsActive);

                    if (fridge != null)
                    {
                        fridgeId = model.SelectedFridgeId.Value;
                        _logger.LogInformation("Selected existing fridge: {FridgeId}", fridgeId);
                    }
                    else
                    {
                        _logger.LogWarning("Selected fridge {FridgeId} not found or doesn't belong to customer", model.SelectedFridgeId);
                    }
                }
                else
                {
                    _logger.LogInformation("No fridge selected for appointment");
                }

                // Handle issue type
                string issueType = model.IssueType;
                if (model.IssueType == "Other" && !string.IsNullOrWhiteSpace(model.OtherIssue))
                {
                    issueType = model.OtherIssue.Trim();
                    _logger.LogInformation("Using custom issue type: {IssueType}", issueType);
                }

                // Update appointment
                appointment.FridgeId = fridgeId;
                appointment.StartTime = model.PreferredDateTime;
                appointment.EndTime = model.PreferredDateTime.AddHours(1);
                appointment.IssueType = issueType ?? "General Service";
                appointment.OtherIssue = model.OtherIssue?.Trim();
                appointment.Notes = model.Notes?.Trim();

                // If technician was assigned, unassign them and reset status
                if (appointment.EmployeeID != null)
                {
                    var defaultEmployee = await GetOrCreateDefaultEmployeeAsync();
                    appointment.EmployeeID = defaultEmployee;
                    appointment.Status = "Incomplete";
                    appointment.Color = "#ffc107"; // Yellow for incomplete
                    _logger.LogInformation("Technician unassigned, status reset to Incomplete");
                }

                _logger.LogInformation("Updating appointment: ID={Id}, Issue={Issue}, Status={Status}",
                    appointment.Id, appointment.IssueType, appointment.Status);

                _applicationContext.Appointments.Update(appointment);
                await _applicationContext.SaveChangesAsync();

                _logger.LogInformation("✅ Appointment updated successfully: ID={Id}", appointment.Id);

                return Json(new
                {
                    success = true,
                    message = "Appointment updated successfully! Our admin team will review the changes.",
                    redirectUrl = Url.Action("MyAppointments", "Services", new { area = "Identity" })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating appointment ID: {Id}. Message: {Message}", id, ex.Message);
                return Json(new
                {
                    success = false,
                    message = $"An error occurred while updating your appointment: {ex.Message}"
                });
            }
        }

        // ======================
        // MY APPOINTMENTS
        // ======================
        public async Task<IActionResult> MyAppointments()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return View(new List<Appointment>());
            }

            // Get appointments from DashboardDbContext (correct)
            var appointments = await _applicationContext.Appointments
                .Include(a => a.Employee)
                // DON'T include Fridge here - it will try to load from DashboardDbContext
                .Where(a => a.CustomerID == customer.CustomerID && !a.IsDeleted)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            // Manually load fridges from ApplicationDbContext
            var fridgeIds = appointments.Where(a => a.FridgeId.HasValue).Select(a => a.FridgeId.Value).Distinct().ToList();

            var fridges = await _applicationContext.FridgeRegistrations
                .Where(f => fridgeIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f);

            // Manually assign fridges to appointments
            foreach (var appointment in appointments)
            {
                if (appointment.FridgeId.HasValue && fridges.ContainsKey(appointment.FridgeId.Value))
                {
                    appointment.Fridge = fridges[appointment.FridgeId.Value];
                }
            }

            return View(appointments);
        }

        // ======================
        // CANCEL APPOINTMENT
        // ======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                var appointment = await _applicationContext.Appointments
                    .FirstOrDefaultAsync(a => a.Id == id && a.CustomerID == customer.CustomerID && !a.IsDeleted);

                if (appointment == null)
                {
                    TempData["Error"] = "Appointment not found.";
                    return RedirectToAction("MyAppointments");
                }

                // Allow cancel even if technician assigned (as long as not completed)
                if (appointment.Status == "Completed")
                {
                    TempData["Error"] = "Cannot cancel a completed appointment.";
                    return RedirectToAction("MyAppointments");
                }

                // HARD DELETE - Remove from database entirely
                _applicationContext.Appointments.Remove(appointment);
                await _applicationContext.SaveChangesAsync();

                TempData["Success"] = "Appointment cancelled and removed successfully.";
                return RedirectToAction("MyAppointments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hard deleting appointment ID: {Id}", id);
                TempData["Error"] = "An error occurred while cancelling the appointment.";
                return RedirectToAction("MyAppointments");
            }
        }

        
        


        // ======================
        // FRIDGE REGISTRATION (STANDALONE)
        // ======================
        [HttpGet]
        public IActionResult RegisterFridge()
        {
            var model = new FridgeRegistrationViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterFridge(FridgeRegistrationViewModel model)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("MyFridges");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var fridge = new FridgeRegistration
                    {
                        CustomerId = customer.CustomerID,
                        FridgeName = model.FridgeName,
                        Brand = model.Brand,
                        PurchaseDate = DateTime.Now, // Add required field
                        IsActive = true,
                        RegistrationDate = DateTime.Now
                    };

                    _applicationContext.FridgeRegistrations.Add(fridge);
                    await _applicationContext.SaveChangesAsync();

                    TempData["Success"] = "Fridge registered successfully!";
                    return RedirectToAction("MyFridges");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error registering fridge");
                    TempData["Error"] = "Error registering fridge. Please try again.";
                }
            }

            return View(model);
        }

        // ======================
        // EDIT FRIDGE
        // ======================
        [HttpGet]
        public async Task<IActionResult> EditFridge(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("MyFridges");
            }

            var fridge = await _applicationContext.FridgeRegistrations
                .FirstOrDefaultAsync(f => f.Id == id && f.CustomerId == customer.CustomerID && f.IsActive);

            if (fridge == null)
            {
                TempData["Error"] = "Fridge not found or access denied.";
                return RedirectToAction("MyFridges");
            }

            var model = new FridgeRegistrationViewModel
            {
                FridgeName = fridge.FridgeName,
                Brand = fridge.Brand
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFridge(int id, FridgeRegistrationViewModel model)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("MyFridges");
            }

            var fridge = await _applicationContext.FridgeRegistrations
                .FirstOrDefaultAsync(f => f.Id == id && f.CustomerId == customer.CustomerID && f.IsActive);

            if (fridge == null)
            {
                TempData["Error"] = "Fridge not found or access denied.";
                return RedirectToAction("MyFridges");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    fridge.FridgeName = model.FridgeName;
                    fridge.Brand = model.Brand;

                    _applicationContext.FridgeRegistrations.Update(fridge);
                    await _applicationContext.SaveChangesAsync();

                    TempData["Success"] = "Fridge updated successfully!";
                    return RedirectToAction("MyFridges");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating fridge");
                    TempData["Error"] = "Error updating fridge. Please try again.";
                }
            }

            return View(model);
        }

        // ======================
        // DELETE FRIDGE
        // ======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFridge(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("MyFridges");
            }

            var fridge = await _applicationContext.FridgeRegistrations
                .FirstOrDefaultAsync(f => f.Id == id && f.CustomerId == customer.CustomerID && f.IsActive);

            if (fridge == null)
            {
                TempData["Error"] = "Fridge not found or access denied.";
                return RedirectToAction("MyFridges");
            }

            try
            {
                fridge.IsActive = false;
                _applicationContext.FridgeRegistrations.Update(fridge);
                await _applicationContext.SaveChangesAsync();

                TempData["Success"] = "Fridge removed successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting fridge");
                TempData["Error"] = "Error removing fridge. Please try again.";
            }

            return RedirectToAction("MyFridges");
        }

        // ======================
        // SERVICE HISTORY BY FRIDGE
        // ======================
        [HttpGet]
        public async Task<IActionResult> ServiceHistory(int fridgeId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("MyFridges");
            }

            // Verify the fridge belongs to the customer
            var fridge = await _applicationContext.FridgeRegistrations
                .FirstOrDefaultAsync(f => f.Id == fridgeId && f.CustomerId == customer.CustomerID);

            if (fridge == null)
            {
                TempData["Error"] = "Fridge not found or access denied.";
                return RedirectToAction("MyFridges");
            }

            var appointments = await _applicationContext.Appointments
                .Include(a => a.Employee)
                .Where(a => a.FridgeId == fridgeId && !a.IsDeleted)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            // Manually assign the fridge to each appointment
            foreach (var appointment in appointments)
            {
                appointment.Fridge = fridge;
            }

            ViewBag.Fridge = fridge;
            return View(appointments);
        }

        // ======================
        // UPCOMING APPOINTMENTS
        // ======================
        [HttpGet]
        public async Task<IActionResult> UpcomingAppointments()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return View(new List<Appointment>());
            }

            var upcomingAppointments = await _applicationContext.Appointments
                .Include(a => a.Employee)
                .Where(a => a.CustomerID == customer.CustomerID &&
                           !a.IsDeleted &&
                           a.StartTime >= DateTime.Now &&
                           (a.Status == "Scheduled" || a.Status == "Incomplete" || a.Status == "Confirmed"))
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            // Manually load fridges
            var fridgeIds = upcomingAppointments.Where(a => a.FridgeId.HasValue).Select(a => a.FridgeId.Value).Distinct().ToList();

            var fridges = await _applicationContext.FridgeRegistrations
                .Where(f => fridgeIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f);

            foreach (var appointment in upcomingAppointments)
            {
                if (appointment.FridgeId.HasValue && fridges.ContainsKey(appointment.FridgeId.Value))
                {
                    appointment.Fridge = fridges[appointment.FridgeId.Value];
                }
            }

            return View(upcomingAppointments);
        }


        // ======================
        // PAST APPOINTMENTS
        // ======================
        [HttpGet]
        public async Task<IActionResult> PastAppointments()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return View(new List<Appointment>());
            }

            var pastAppointments = await _applicationContext.Appointments
                .Include(a => a.Employee)
                .Where(a => a.CustomerID == customer.CustomerID &&
                           !a.IsDeleted &&
                           a.StartTime < DateTime.Now)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            // Manually load fridges
            var fridgeIds = pastAppointments.Where(a => a.FridgeId.HasValue).Select(a => a.FridgeId.Value).Distinct().ToList();

            var fridges = await _applicationContext.FridgeRegistrations
                .Where(f => fridgeIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f);

            foreach (var appointment in pastAppointments)
            {
                if (appointment.FridgeId.HasValue && fridges.ContainsKey(appointment.FridgeId.Value))
                {
                    appointment.Fridge = fridges[appointment.FridgeId.Value];
                }
            }

            return View(pastAppointments);
        }

        // ======================
        // HELPER METHODS (UPDATED)
        // ======================
        private async Task<Customer> GetCurrentCustomerAsync()
        {
            return await GetCurrentCustomerAsync(null);
        }

        private async Task<Customer> GetCurrentCustomerAsync(ApplicationUser currentUser)
        {
            try
            {
                var identityUserId = currentUser?.Id ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(identityUserId))
                {
                    _logger.LogWarning("No IdentityUserId found in claims");
                    return null;
                }

                _logger.LogInformation("Looking up customer for IdentityUserId: {Id}", identityUserId);

                // Try ApplicationDbContext FIRST (where registrations are)
                var customer = await _applicationContext.Customers
                    .FirstOrDefaultAsync(c => c.IdentityUserId == identityUserId && !c.IsDeleted);

                if (customer == null)
                {
                    // Fallback to DashboardContext
                    customer = await _applicationContext.Customers
                        .FirstOrDefaultAsync(c => c.IdentityUserId == identityUserId && !c.IsDeleted);
                }

                if (customer != null)
                {
                    _logger.LogInformation("Found customer: {CustomerId}", customer.CustomerID);
                }
                else
                {
                    _logger.LogWarning("No customer found for user");
                }

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current customer");
                return null;
            }
        }

        private async Task<int> GetOrCreateDefaultEmployeeAsync()
        {
            try
            {
                // Try to find an existing technician first
                var existingEmployee = await _applicationContext.Employees
                    .FirstOrDefaultAsync(e => e.JobTitle == "Technician" && e.IsActive && !e.IsDeleted);

                if (existingEmployee != null)
                {
                    _logger.LogInformation("Found existing technician: {EmployeeId}", existingEmployee.EmployeeID);
                    return existingEmployee.EmployeeID;
                }

                // Create a default "Unassigned" technician if none exists
                var placeholder = new Employee
                {
                    FirstName = "Unassigned",
                    LastName = "Technician",
                    JobTitle = "Technician",
                    Position = "Technician",
                    ContactNumber = "0000000000",
                    Email = "unassigned@fridgefrenzy.com",
                    IsActive = true,
                    IsDeleted = false
                };

                _applicationContext.Employees.Add(placeholder);
                await _applicationContext.SaveChangesAsync();

                _logger.LogInformation("Created placeholder technician: {EmployeeId}", placeholder.EmployeeID);
                return placeholder.EmployeeID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateDefaultEmployeeAsync");
                throw new InvalidOperationException("Failed to get or create default employee", ex);
            }
        }

        private DateTime GetNextAvailableDateTime()
        {
            var next = DateTime.Now.AddDays(1).Date.AddHours(8);
            if (next.DayOfWeek == DayOfWeek.Sunday)
                next = next.AddDays(1);
            return next;
        }

        private List<string> ValidateAppointmentDateTime(DateTime dateTime)
        {
            var errors = new List<string>();
            if (dateTime.Date <= DateTime.Today)
                errors.Add("Appointments must be booked from tomorrow onwards.");
            if (dateTime.DayOfWeek == DayOfWeek.Sunday)
                errors.Add("Appointments cannot be booked on Sundays.");
            if (dateTime.Hour < 8 || dateTime.Hour >= 16)
                errors.Add("Appointments must be scheduled between 8:00 AM and 4:00 PM.");
            return errors;
        }

        private bool IsAjaxRequest()
        {
            var requestedWith = Request.Headers["X-Requested-With"].ToString();
            return !string.IsNullOrEmpty(requestedWith) && requestedWith == "XMLHttpRequest";
        }

        // Get fridge statistics for dashboard
        public async Task<FridgeStatsViewModel> GetFridgeStats(int customerId)
        {
            var fridges = await _applicationContext.FridgeRegistrations
                .Where(f => f.CustomerId == customerId && f.IsActive)
                .ToListAsync();

            var appointments = await _applicationContext.Appointments
                .Where(a => a.FridgeId.HasValue && fridges.Select(f => f.Id).Contains(a.FridgeId.Value) && !a.IsDeleted)
                .ToListAsync();

            return new FridgeStatsViewModel
            {
                TotalFridges = fridges.Count,
                UnderWarranty = 0, // Removed warranty tracking
                TotalServices = appointments.Count,
                RecentServices = appointments.Count(a => a.StartTime >= DateTime.Now.AddMonths(-6))
            };
        }

        // Quick fridge registration for appointments
        public async Task<IActionResult> QuickRegisterFridge(string model, string brand, string serialNumber = null)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Json(new { success = false, message = "Customer not found" });

            var fridge = new FridgeRegistration
            {
                CustomerId = customer.CustomerID,
                FridgeName = model,
                Brand = brand,
                SerialNumber = serialNumber,
                PurchaseDate = DateTime.Now, // Add required field
                IsActive = true,
                RegistrationDate = DateTime.Now
            };

            _applicationContext.FridgeRegistrations.Add(fridge);
            await _applicationContext.SaveChangesAsync();

            return Json(new { success = true, fridgeId = fridge.Id, fridgeName = $"{brand} {model}" });
        }
        // ======================
        // DEBUG METHOD - Check Fridge Registration
        // ======================
        [HttpGet]
        public async Task<IActionResult> DebugFridges()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Content("No customer found");
            }

            var fridges = await _applicationContext.FridgeRegistrations
                .Where(f => f.CustomerId == customer.CustomerID)
                .ToListAsync();

            var result = $"Customer ID: {customer.CustomerID}<br/>";
            result += $"Total Fridges: {fridges.Count}<br/>";

            foreach (var fridge in fridges)
            {
                result += $"Fridge: {fridge.FridgeName} (ID: {fridge.Id}, Active: {fridge.IsActive})<br/>";
            }

            return Content(result);
        }
        [HttpGet]
        public async Task<IActionResult> DebugFridgeRegistration()
        {
            var customer = await GetCurrentCustomerAsync();

            var result = "<h3>Debug Information</h3>";
            result += $"<p><strong>Customer Found:</strong> {customer != null}</p>";

            if (customer != null)
            {
                result += $"<p><strong>Customer ID:</strong> {customer.CustomerID}</p>";
                result += $"<p><strong>Customer Name:</strong> {customer.Name}</p>";

                var fridges = await _applicationContext.FridgeRegistrations
                    .Where(f => f.CustomerId == customer.CustomerID)
                    .ToListAsync();

                result += $"<p><strong>Total Fridges in ApplicationDbContext:</strong> {fridges.Count}</p>";

                if (fridges.Any())
                {
                    result += "<ul>";
                    foreach (var fridge in fridges)
                    {
                        result += $"<li>ID: {fridge.Id}, Name: {fridge.FridgeName}, Brand: {fridge.Brand}, Active: {fridge.IsActive}</li>";
                    }
                    result += "</ul>";
                }
            }

            return Content(result, "text/html");
        }
    }
}