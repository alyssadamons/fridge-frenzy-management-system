// Areas/Dashboard/Controllers/LogsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using E_Commerce.Services;
using E_Commerce.Models;

namespace E_Commerce.Areas.Dashboard.Controllers
{
    [Area("Dashboard")]
    [Authorize(Roles = "Admin")]
    public class LogsController : Controller
    {
        private readonly ILoggingService _loggingService;

        public LogsController(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public async Task<IActionResult> Index(string actionFilter = null,
                                              DateTime? fromDate = null,
                                              DateTime? toDate = null,
                                              int page = 1)
        {
            var logs = await _loggingService.GetLogsAsync(fromDate, toDate, actionFilter, page);
            var totalCount = await _loggingService.GetTotalLogCountAsync();

            ViewBag.ActionFilter = actionFilter;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.CurrentPage = page;
            ViewBag.TotalLogs = totalCount;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = logs.Count == 50; // If we got a full page, there might be more

            return View(logs);
        }
    }
}