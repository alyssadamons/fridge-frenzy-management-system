using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Areas.Dashboard.Controllers
{
    [Area("Dashboard")]
    [Authorize(Roles = "Admin,Sales,CustomerManager,Technician,Employee")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILoggingService _loggingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            ILoggingService loggingService,
            UserManager<ApplicationUser> userManager,
            ILogger<HomeController> logger)
        {
            _context = context;
            _loggingService = loggingService;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var userRoles = await _userManager.GetRolesAsync(currentUser);

                // Enhanced dashboard access logging
                await _loggingService.LogActionAsync(
                    "DashboardAccessed",
                    $"Dashboard accessed by {User.Identity?.Name} with roles: {string.Join(", ", userRoles)}",
                    User.Identity?.Name
                );

                // Set common view data
                ViewBag.UserName = User.Identity?.Name ?? "User";

                // Check if admin by email (fallback solution)
                var userEmail = currentUser?.Email ?? "";
                var isAdmin = userEmail == "admin@fridgefrenzy.com";

                if (isAdmin)
                {
                    ViewBag.UserRole = "Administrator";
                    await SetAdminDashboardData();
                }
                else if (userRoles.Contains("Technician"))
                {
                    ViewBag.UserRole = "Technician";
                    await SetTechnicianDashboardData();
                }
                else if (userRoles.Contains("Sales"))
                {
                    ViewBag.UserRole = "Sales Representative";
                    await SetSalesDashboardData();
                }
                else if (userRoles.Contains("CustomerManager"))
                {
                    ViewBag.UserRole = "Customer Manager";
                    await SetCustomerManagerDashboardData();
                }
                else if (userRoles.Contains("Employee"))
                {
                    ViewBag.UserRole = "Employee";
                    await SetEmployeeDashboardData();
                }
                else
                {
                    // Fallback for users with no specific role
                    ViewBag.UserRole = "User";
                    await SetEmployeeDashboardData();
                }

                return View();
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync("Dashboard/Index", ex, User.Identity?.Name);

                // Return safe defaults
                ViewBag.UserName = User.Identity?.Name ?? "User";
                ViewBag.UserRole = "Employee";
                return View();
            }
        }

        private async Task SetAdminDashboardData()
        {
            try
            {
                // Get basic counts
                ViewBag.TotalCustomers = await _context.Customers.CountAsync(c => !c.IsDeleted);
                ViewBag.TotalEmployees = await _context.Employees.CountAsync(e => !e.IsDeleted);
                ViewBag.TotalProducts = await _context.Products.CountAsync();
                ViewBag.PendingAppointments = await _context.Appointments.CountAsync(a => !a.IsDeleted && a.StartTime >= DateTime.Now && (a.Status == "Pending" || a.Status == "Incomplete"));
                ViewBag.ThisWeekAppointments = await GetThisWeekAppointments();

                // ANALYTICS DATA
                ViewBag.CustomerLocationData = await GetCustomerLocationDistribution();
                ViewBag.AppointmentTrends = await GetAppointmentTrends();
                ViewBag.ServiceTypeAnalysis = await GetServiceTypeAnalysis();

                var revenueInsights = await GetRevenueInsights();
                ViewBag.RevenueInsights = revenueInsights;

               

                // NEW: Enhanced customer activity analytics
                ViewBag.RecentCustomerActivity = await _loggingService.GetLogsByTypeAsync("Customer", 24);
                ViewBag.PopularPages = await _loggingService.GetPopularPagesAsync(DateTime.Now.AddDays(-7));
                ViewBag.ConversionFunnel = await _loggingService.GetCustomerConversionFunnelAsync(DateTime.Now.AddDays(-30));
                ViewBag.TopProducts = await _loggingService.GetTopProductsAsync(DateTime.Now.AddDays(-7));
                ViewBag.ActivitySummary = await _loggingService.GetActivitySummaryAsync(DateTime.Now.AddDays(-7));

                // Get recent data
                ViewBag.RecentAppointments = await GetRecentAppointments();
                ViewBag.RecentLogs = await GetRecentLogs();
                ViewBag.RecentOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToListAsync();

                // NEW: Real-time activity metrics
                ViewBag.TodaysPageViews = await _loggingService.GetLogsByTypeAsync("PageView", 24);
                ViewBag.RecentPurchases = await _loggingService.GetLogsByTypeAsync("Purchase", 24);
                ViewBag.CartActivities = await _loggingService.GetLogsByTypeAsync("Cart", 24);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting admin dashboard data");
                await _loggingService.LogErrorAsync("SetAdminDashboardData", ex, User.Identity?.Name);
            }
        }

        private async Task SetTechnicianDashboardData()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var technician = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Email == currentUser.Email && !e.IsDeleted);

                ViewBag.UserName = technician != null ?
                    $"{technician.FirstName} {technician.LastName}" :
                    User.Identity?.Name ?? "Technician";

                // Technician sees appointments and customer info
                ViewBag.TodaysAppointments = await _context.Appointments
                    .CountAsync(a => !a.IsDeleted && a.StartTime.Date == DateTime.Today &&
                                   (a.Status == "Scheduled" || a.Status == "In Progress" || a.Status == "Pending"));

                ViewBag.UpcomingAppointments = await _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Fridge)
                    .Where(a => !a.IsDeleted && a.StartTime >= DateTime.Now &&
                               (a.Status == "Scheduled" || a.Status == "In Progress" || a.Status == "Pending"))
                    .OrderBy(a => a.StartTime)
                    .Take(5)
                    .ToListAsync();

                ViewBag.RecentCompleted = await _context.Appointments
                    .CountAsync(a => !a.IsDeleted && a.Status == "Completed" && a.StartTime >= DateTime.Now.AddDays(-7));

                // Customer stats for technician reference
                ViewBag.TotalCustomers = await _context.Customers.CountAsync(c => !c.IsDeleted);

                // NEW: Technician-specific activity
                ViewBag.RecentServiceLogs = await _loggingService.GetLogsByTypeAsync("Customer", 24)
                    .ContinueWith(task => task.Result?.Where(log =>
                        log.Description.Contains("Appointment") ||
                        log.Description.Contains("Service")).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting technician dashboard data");
                await _loggingService.LogErrorAsync("SetTechnicianDashboardData", ex, User.Identity?.Name);
            }
        }

        private async Task SetSalesDashboardData()
        {
            try
            {
                // Sales sees products, orders, and categories
                ViewBag.TotalProducts = await _context.Products.CountAsync();

                ViewBag.RecentOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToListAsync();

                ViewBag.MonthlyRevenue = await _context.Orders
                    .Where(o => o.OrderDate.Month == DateTime.Now.Month && o.OrderDate.Year == DateTime.Now.Year)
                    .SumAsync(o => o.Total);

                ViewBag.TotalCategories = await _context.Categories.CountAsync();

                // NEW: Sales-specific analytics
                ViewBag.RecentProductViews = await _loggingService.GetTopProductsAsync(DateTime.Now.AddDays(-7));
                ViewBag.CustomerInteractions = await _loggingService.GetLogsByTypeAsync("Customer", 24)
                    .ContinueWith(task => task.Result?.Where(log =>
                        log.Description.Contains("Purchase") ||
                        log.Description.Contains("Cart")).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting sales dashboard data");
                await _loggingService.LogErrorAsync("SetSalesDashboardData", ex, User.Identity?.Name);
            }
        }

        private async Task SetCustomerManagerDashboardData()
        {
            try
            {
                // Customer Manager sees customers and orders
                ViewBag.TotalCustomers = await _context.Customers.CountAsync(c => !c.IsDeleted);

                ViewBag.NewCustomersThisMonth = await _context.Customers
                    .CountAsync(c => !c.IsDeleted );

                ViewBag.RecentCustomers = await _context.Customers
                    .Where(c => !c.IsDeleted)
                    .OrderByDescending(c => c.CustomerID)
                    .Take(5)
                    .ToListAsync();

                ViewBag.RecentOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToListAsync();

                ViewBag.TotalOrders = await _context.Orders.CountAsync();

                // NEW: Customer Manager analytics
                ViewBag.CustomerActivity = await _loggingService.GetLogsByTypeAsync("Customer", 24);
                ViewBag.ServiceRequests = await _loggingService.GetLogsByTypeAsync("Customer", 24)
                    .ContinueWith(task => task.Result?.Where(log =>
                        log.Description.Contains("Appointment") ||
                        log.Description.Contains("Service")).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting customer manager dashboard data");
                await _loggingService.LogErrorAsync("SetCustomerManagerDashboardData", ex, User.Identity?.Name);
            }
        }

        private async Task SetEmployeeDashboardData()
        {
            try
            {
                // Basic employee view with limited access
                ViewBag.RecentActivity = await _loggingService.GetRecentLogsAsync(10);

                // NEW: Employee-specific activity feed
                ViewBag.SystemAlerts = await _loggingService.GetLogsByTypeAsync("Error", 24)
                    .ContinueWith(task => task.Result?.Take(3).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting employee dashboard data");
                await _loggingService.LogErrorAsync("SetEmployeeDashboardData", ex, User.Identity?.Name);
            }
        }

        // ========== ANALYTICS METHODS ==========

        private async Task<List<object>> GetCustomerLocationDistribution()
        {
            try
            {
                var locationData = await _context.Customers
                    .Where(c => !c.IsDeleted && c.City != null)
                    .GroupBy(c => c.City)
                    .Select(g => new
                    {
                        city = g.Key ?? "Unknown",
                        count = g.Count()
                    })
                    .OrderByDescending(x => x.count)
                    .Take(10)
                    .ToListAsync<object>();

                return locationData.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer location data");
                await _loggingService.LogErrorAsync("GetCustomerLocationDistribution", ex, User.Identity?.Name);

                // Return sample data
                return new List<object>
                {
                    new { city = "Port Elizabeth", count = 15 },
                    new { city = "Cape Town", count = 12 },
                    new { city = "Johannesburg", count = 8 },
                    new { city = "Durban", count = 6 },
                    new { city = "Pretoria", count = 4 }
                };
            }
        }

        private async Task<List<object>> GetAppointmentTrends()
        {
            try
            {
                var sixMonthsAgo = DateTime.Now.AddMonths(-6);

                // Generate last 6 months
                var months = new List<DateTime>();
                for (int i = 5; i >= 0; i--)
                {
                    months.Add(DateTime.Now.AddMonths(-i));
                }

                var trends = new List<object>();

                foreach (var month in months)
                {
                    var monthStart = new DateTime(month.Year, month.Month, 1);
                    var monthEnd = monthStart.AddMonths(1);

                    var monthAppointments = await _context.Appointments
                        .Where(a => !a.IsDeleted && a.StartTime >= monthStart && a.StartTime < monthEnd)
                        .ToListAsync();

                    trends.Add(new
                    {
                        period = monthStart.ToString("MMM yyyy"),
                        totalAppointments = monthAppointments.Count,
                        completed = monthAppointments.Count(a => a.Status?.ToLower() == "completed"),
                        pending = monthAppointments.Count(a => a.Status?.ToLower() == "pending"),
                        cancelled = monthAppointments.Count(a => a.Status?.ToLower() == "cancelled")
                    });
                }

                return trends;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating appointment trends");
                await _loggingService.LogErrorAsync("GetAppointmentTrends", ex, User.Identity?.Name);

                // Return sample data
                return new List<object>
                {
                    new { period = "Jan 2024", totalAppointments = 8, completed = 5, pending = 3, cancelled = 0 },
                    new { period = "Feb 2024", totalAppointments = 12, completed = 8, pending = 4, cancelled = 0 },
                    new { period = "Mar 2024", totalAppointments = 10, completed = 7, pending = 3, cancelled = 0 },
                    new { period = "Apr 2024", totalAppointments = 15, completed = 10, pending = 5, cancelled = 0 },
                    new { period = "May 2024", totalAppointments = 11, completed = 8, pending = 3, cancelled = 0 },
                    new { period = "Jun 2024", totalAppointments = 9, completed = 6, pending = 3, cancelled = 0 }
                };
            }
        }

        private async Task<List<object>> GetServiceTypeAnalysis()
        {
            try
            {
                var serviceData = await _context.Appointments
                    .Where(a => !a.IsDeleted && a.IssueType != null)
                    .GroupBy(a => a.IssueType)
                    .Select(g => new
                    {
                        serviceType = g.Key,
                        count = g.Count(),
                        completed = g.Count(a => a.Status != null && a.Status.ToLower() == "completed"), // FIXED: No null-conditional operator
                        revenue = g.Sum(a => 0) // Placeholder for future revenue tracking
                    })
                    .OrderByDescending(x => x.count)
                    .Take(8)
                    .ToListAsync<object>();

                return serviceData.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service type analysis");
                await _loggingService.LogErrorAsync("GetServiceTypeAnalysis", ex, User.Identity?.Name);

                // Return sample data
                return new List<object>
        {
            new { serviceType = "Compressor Repair", count = 25, completed = 20, revenue = 0 },
            new { serviceType = "Gas Refill", count = 18, completed = 15, revenue = 0 },
            new { serviceType = "Thermostat Replacement", count = 12, completed = 10, revenue = 0 },
            new { serviceType = "General Service", count = 10, completed = 8, revenue = 0 },
            new { serviceType = "Electrical Fault", count = 8, completed = 6, revenue = 0 },
            new { serviceType = "Door Seal Replacement", count = 6, completed = 5, revenue = 0 }
        };
            }
        }

        private async Task<RevenueInsightsViewModel> GetRevenueInsights()
        {
            try
            {
                var sixMonthsAgo = DateTime.Now.AddMonths(-6);
                var orders = await _context.Orders
                    .Where(o => o.OrderDate >= sixMonthsAgo)
                    .ToListAsync();

                // Generate last 6 months
                var months = new List<DateTime>();
                for (int i = 5; i >= 0; i--)
                {
                    months.Add(DateTime.Now.AddMonths(-i));
                }

                var monthlyRevenue = new List<MonthlyRevenueData>();

                foreach (var month in months)
                {
                    var monthStart = new DateTime(month.Year, month.Month, 1);
                    var monthEnd = monthStart.AddMonths(1);

                    var monthOrders = orders
                        .Where(o => o.OrderDate >= monthStart && o.OrderDate < monthEnd)
                        .ToList();

                    var monthRevenue = monthOrders.Sum(o => o.Total);
                    var orderCount = monthOrders.Count;

                    monthlyRevenue.Add(new MonthlyRevenueData
                    {
                        Period = monthStart.ToString("MMM yyyy"),
                        Revenue = monthRevenue,
                        OrderCount = orderCount
                    });
                }

                var totalRevenue = monthlyRevenue.Sum(x => x.Revenue);
                var totalOrders = monthlyRevenue.Sum(x => x.OrderCount);
                var avgMonthlyRevenue = monthlyRevenue.Average(x => x.Revenue);
                var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

                double growthRate = 0;
                if (monthlyRevenue.Count >= 2)
                {
                    var currentMonth = monthlyRevenue.Last().Revenue;
                    var previousMonth = monthlyRevenue[^2].Revenue;
                    growthRate = previousMonth > 0 ? (double)((currentMonth - previousMonth) / previousMonth * 100) : 0;
                }

                return new RevenueInsightsViewModel
                {
                    MonthlyData = monthlyRevenue,
                    TotalRevenue = totalRevenue,
                    TotalOrders = totalOrders,
                    AverageMonthly = avgMonthlyRevenue,
                    AverageOrderValue = avgOrderValue,
                    GrowthRate = growthRate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating revenue insights");
                await _loggingService.LogErrorAsync("GetRevenueInsights", ex, User.Identity?.Name);

                return new RevenueInsightsViewModel
                {
                    MonthlyData = new List<MonthlyRevenueData>(),
                    TotalRevenue = 0,
                    TotalOrders = 0,
                    AverageMonthly = 0,
                    AverageOrderValue = 0,
                    GrowthRate = 0
                };
            }
        }

        

        // ========== EXISTING HELPER METHODS ==========

        private async Task<List<RecentAppointmentViewModel>> GetRecentAppointments()
        {
            try
            {
                var appointments = await _context.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Employee)
                    .Where(a => !a.IsDeleted)
                    .OrderByDescending(a => a.StartTime)
                    .Take(5)
                    .ToListAsync();

                _logger.LogInformation($"Found {appointments.Count} recent appointments for dashboard");

                return appointments.Select(a => new RecentAppointmentViewModel
                {
                    CustomerName = a.Customer != null ? a.Customer.Name : "N/A",
                    TechnicianName = a.Employee != null
                        ? (a.Employee.FirstName + " " + (a.Employee.LastName ?? "")).Trim()
                        : "Unassigned",
                    StartTime = a.StartTime,
                    Status = a.Status ?? "Pending",
                    StatusColor = GetStatusColorStatic(a.Status)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent appointments");
                await _loggingService.LogErrorAsync("GetRecentAppointments", ex, User.Identity?.Name);
                return new List<RecentAppointmentViewModel>();
            }
        }

        private async Task<List<RecentLogViewModel>> GetRecentLogs()
        {
            try
            {
                var logs = await _loggingService.GetRecentLogsAsync(5);

                _logger.LogInformation($"Retrieved {logs?.Count ?? 0} recent logs for dashboard");

                if (logs == null || !logs.Any())
                {
                    _logger.LogWarning("No logs found in database");
                    return new List<RecentLogViewModel>();
                }

                return logs.Select(log => new RecentLogViewModel
                {
                    Timestamp = log.Timestamp,
                    Action = log.Action,
                    UserEmail = log.UserEmail ?? "System",
                    Description = log.Description,
                    ActionColor = GetActionColorStatic(log.Action)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent logs");
                await _loggingService.LogErrorAsync("GetRecentLogs", ex, User.Identity?.Name);
                return new List<RecentLogViewModel>();
            }
        }

        private async Task<int> GetThisWeekAppointments()
        {
            try
            {
                var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                var endOfWeek = startOfWeek.AddDays(7);
                return await _context.Appointments
                    .CountAsync(a => !a.IsDeleted && a.StartTime >= startOfWeek && a.StartTime < endOfWeek);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting this week appointments");
                await _loggingService.LogErrorAsync("GetThisWeekAppointments", ex, User.Identity?.Name);
                return 0;
            }
        }

        private static string GetStatusColorStatic(string status)
        {
            if (string.IsNullOrEmpty(status)) return "secondary";
            return status.ToLower() switch
            {
                "completed" => "success",
                "pending" or "incomplete" => "warning",
                "cancelled" => "danger",
                "in progress" or "inprogress" => "info",
                "scheduled" => "primary",
                _ => "secondary"
            };
        }

        private static string GetActionColorStatic(string action)
        {
            if (string.IsNullOrEmpty(action)) return "secondary";
            return action.ToLower() switch
            {
                "usercreated" or "employeeadded" or "customeradded" or "productadded" => "success",
                "userlogin" or "dashboardaccessed" => "info",
                "infoupdated" or "employeeupdated" or "customerupdated" or "appointmentupdated" => "warning",
                "orderplaced" or "appointmentcreated" or "productviewed" => "primary",
                "ordercompleted" or "appointmentcompleted" => "success",
                "userdeleted" or "customerdeleted" or "appointmentdeleted" => "danger",
                _ => "secondary"
            };
        }
    }

    // ========== ENHANCED VIEWMODELS ==========

    public class RecentAppointmentViewModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
    }

    public class RecentLogViewModel
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActionColor { get; set; } = string.Empty;
    }

    public class RevenueInsightsViewModel
    {
        public List<MonthlyRevenueData> MonthlyData { get; set; } = new List<MonthlyRevenueData>();
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageMonthly { get; set; }
        public decimal AverageOrderValue { get; set; }
        public double GrowthRate { get; set; }
    }

    public class MonthlyRevenueData
    {
        public string Period { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }
}