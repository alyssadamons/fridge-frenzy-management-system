// Enhanced ILoggingService interface
using E_Commerce.Models;
using E_Commerce.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace E_Commerce.Services
{
    public interface ILoggingService
    {
        // Existing methods
        Task LogActionAsync(string action, string description, string userEmail = null,
                          string affectedUserEmail = null, string affectedUserName = null,
                          string additionalData = null);
        Task<List<AppLog>> GetLogsAsync(DateTime? fromDate = null, DateTime? toDate = null,
                                       string action = null, int page = 1, int pageSize = 50);
        Task<int> GetTotalLogCountAsync();
        Task<List<AppLog>> GetRecentLogsAsync(int count = 10);

        // NEW: Enhanced logging methods for comprehensive tracking
        Task LogCustomerActionAsync(string action, string description, string userEmail = null, string additionalData = null);
        Task LogSecurityEventAsync(string eventType, string description, string userEmail = null, string additionalData = null);
        Task LogPageViewAsync(string page, string userEmail = null, string additionalData = null);
        Task LogErrorAsync(string context, Exception exception, string userEmail = null, string additionalData = null);
        Task LogPurchaseActivityAsync(string action, decimal amount, string details, string userEmail = null, string additionalData = null);
        Task LogProductActivityAsync(string action, int productId, string productName, string userEmail = null, string additionalData = null);
        Task LogCartActivityAsync(string action, int? productId, string productName, int quantity, string userEmail = null, string additionalData = null);

        // NEW: Analytics methods
        Task<List<AppLog>> GetUserActivityAsync(string userEmail, DateTime? startDate = null);
        Task<List<AppLog>> GetLogsByTypeAsync(string logType, int hoursBack = 24);
        Task<List<object>> GetPopularPagesAsync(DateTime? startDate = null);
        Task<List<object>> GetCustomerConversionFunnelAsync(DateTime? startDate = null);
        Task<List<object>> GetTopProductsAsync(DateTime? startDate = null);
        Task<Dictionary<string, int>> GetActivitySummaryAsync(DateTime? startDate = null);
    }

    public class EnhancedLoggingService : ILoggingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EnhancedLoggingService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // ========== CORE LOGGING METHODS ==========

        public async Task LogActionAsync(string action, string description, string userEmail = null,
                                       string affectedUserEmail = null, string affectedUserName = null,
                                       string additionalData = null)
        {
            await LogToDatabase("Admin", action, description, userEmail, affectedUserEmail, affectedUserName, additionalData);
        }

        public async Task LogCustomerActionAsync(string action, string description, string userEmail = null, string additionalData = null)
        {
            await LogToDatabase("Customer", action, description, userEmail, null, null, additionalData);
        }

        public async Task LogSecurityEventAsync(string eventType, string description, string userEmail = null, string additionalData = null)
        {
            await LogToDatabase("Security", eventType, description, userEmail, null, null, additionalData);
        }

        public async Task LogPageViewAsync(string page, string userEmail = null, string additionalData = null)
        {
            await LogToDatabase("PageView", "PageAccessed", $"Accessed page: {page}", userEmail, null, null, additionalData);
        }

        public async Task LogErrorAsync(string context, Exception exception, string userEmail = null, string additionalData = null)
        {
            var description = $"Error in {context}: {exception.Message}";
            if (exception.InnerException != null)
            {
                description += $" | Inner: {exception.InnerException.Message}";
            }

            // Include stack trace for debugging
            var fullAdditionalData = $"Stack Trace: {exception.StackTrace}";
            if (!string.IsNullOrEmpty(additionalData))
            {
                fullAdditionalData += $" | {additionalData}";
            }

            await LogToDatabase("Error", "SystemError", description, userEmail, null, null, fullAdditionalData);
        }

        public async Task LogPurchaseActivityAsync(string action, decimal amount, string details, string userEmail = null, string additionalData = null)
        {
            var description = $"{action} - Amount: R{amount:N2} - {details}";
            await LogToDatabase("Purchase", action, description, userEmail, null, null, additionalData);
        }

        public async Task LogProductActivityAsync(string action, int productId, string productName, string userEmail = null, string additionalData = null)
        {
            var description = $"{action} - Product: {productName} (ID: {productId})";
            await LogToDatabase("Product", action, description, userEmail, null, null, additionalData);
        }

        public async Task LogCartActivityAsync(string action, int? productId, string productName, int quantity, string userEmail = null, string additionalData = null)
        {
            var productInfo = productId.HasValue ? $"{productName} (ID: {productId})" : "Multiple items";
            var description = $"{action} - {productInfo} - Qty: {quantity}";
            await LogToDatabase("Cart", action, description, userEmail, null, null, additionalData);
        }

        // ========== DATABASE LOGGING ==========

        private async Task LogToDatabase(string logType, string action, string description,
                                       string userEmail = null, string affectedUserEmail = null,
                                       string affectedUserName = null, string additionalData = null)
        {
            try
            {
                var logEntry = new AppLog
                {
                    Action = action,
                    Description = $"[{logType}] {description}",
                    UserEmail = userEmail ?? GetCurrentUserEmail(),
                    AffectedUserEmail = affectedUserEmail ?? string.Empty,
                    AffectedUserName = affectedUserName ?? string.Empty,
                    AdditionalData = additionalData ?? string.Empty,
                    Timestamp = DateTime.Now
                };

                _context.AppLogs.Add(logEntry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Fallback to console if database logging fails
                Console.WriteLine($"LOGGING FAILED: {logType} - {action} - {description} - Error: {ex.Message}");

                // Try to log the logging failure (meta-logging)
                try
                {
                    var fallbackLog = new AppLog
                    {
                        Action = "LoggingFailure",
                        Description = $"[System] Failed to log: {logType} - {action} - Original error: {ex.Message}",
                        UserEmail = "System",
                        Timestamp = DateTime.Now
                    };
                    _context.AppLogs.Add(fallbackLog);
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // If even meta-logging fails, just give up
                }
            }
        }

        private string GetCurrentUserEmail()
        {
            try
            {
                return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
            }
            catch
            {
                return "System";
            }
        }

        // ========== ANALYTICS METHODS ==========

        public async Task<List<AppLog>> GetUserActivityAsync(string userEmail, DateTime? startDate = null)
        {
            var query = _context.AppLogs.Where(l => l.UserEmail == userEmail);

            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value);
            }

            return await query.OrderByDescending(l => l.Timestamp).ToListAsync();
        }

        public async Task<List<AppLog>> GetLogsByTypeAsync(string logType, int hoursBack = 24)
        {
            var cutoff = DateTime.Now.AddHours(-hoursBack);

            return await _context.AppLogs
                .Where(l => l.Description.StartsWith($"[{logType}]") && l.Timestamp >= cutoff)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<List<object>> GetPopularPagesAsync(DateTime? startDate = null)
        {
            var query = _context.AppLogs.Where(l => l.Description.StartsWith("[PageView]"));

            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value);
            }

            return await query
                .GroupBy(l => l.Description)
                .Select(g => new
                {
                    Page = g.Key.Replace("[PageView] Accessed page: ", ""),
                    Count = g.Count(),
                    LastAccess = g.Max(l => l.Timestamp)
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync<object>();
        }

        public async Task<List<object>> GetCustomerConversionFunnelAsync(DateTime? startDate = null)
        {
            var query = _context.AppLogs.Where(l =>
                l.Description.StartsWith("[Customer]") ||
                l.Description.StartsWith("[Purchase]") ||
                l.Description.StartsWith("[Cart]"));

            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value);
            }

            var funnelData = await query
                .GroupBy(l => l.Action)
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .ToListAsync<object>();

            return funnelData;
        }

        public async Task<List<object>> GetTopProductsAsync(DateTime? startDate = null)
        {
            var query = _context.AppLogs.Where(l => l.Description.StartsWith("[Product]"));

            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value);
            }

            return await query
                .GroupBy(l => l.Description)
                .Select(g => new
                {
                    Product = g.Key.Replace("[Product] ", ""),
                    Views = g.Count(l => l.Action == "ProductViewed"),
                    CartAdds = g.Count(l => l.Action == "ProductAddedToCart"),
                    LastActivity = g.Max(l => l.Timestamp)
                })
                .OrderByDescending(x => x.Views + x.CartAdds)
                .Take(10)
                .ToListAsync<object>();
        }

        public async Task<Dictionary<string, int>> GetActivitySummaryAsync(DateTime? startDate = null)
        {
            var query = _context.AppLogs.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value);
            }

            var summary = new Dictionary<string, int>
            {
                ["TotalLogs"] = await query.CountAsync(),
                ["CustomerActions"] = await query.CountAsync(l => l.Description.StartsWith("[Customer]")),
                ["PageViews"] = await query.CountAsync(l => l.Description.StartsWith("[PageView]")),
                ["Purchases"] = await query.CountAsync(l => l.Description.StartsWith("[Purchase]")),
                ["CartActivities"] = await query.CountAsync(l => l.Description.StartsWith("[Cart]")),
                ["ProductViews"] = await query.CountAsync(l => l.Description.StartsWith("[Product]") && l.Action == "ProductViewed"),
                ["Errors"] = await query.CountAsync(l => l.Description.StartsWith("[Error]"))
            };

            return summary;
        }

        // ========== EXISTING METHODS ==========

        public async Task<List<AppLog>> GetLogsAsync(DateTime? fromDate = null, DateTime? toDate = null,
                                                    string action = null, int page = 1, int pageSize = 50)
        {
            var query = _context.AppLogs.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(l => l.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(l => l.Timestamp <= toDate.Value);

            if (!string.IsNullOrEmpty(action))
                query = query.Where(l => l.Action == action);

            return await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalLogCountAsync()
        {
            return await _context.AppLogs.CountAsync();
        }

        public async Task<List<AppLog>> GetRecentLogsAsync(int count = 10)
        {
            return await _context.AppLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(count)
                .ToListAsync();
        }
    }
}