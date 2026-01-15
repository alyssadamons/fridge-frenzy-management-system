using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace E_Commerce.Areas.Dashboard.Controllers
{
    [Area("Dashboard")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILoggingService _loggingService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(ApplicationDbContext context, ILoggingService loggingService, ILogger<OrdersController> logger)
        {
            _context = context;
            _loggingService = loggingService;
            _logger = logger;
        }

        // GET: Dashboard/Orders
        public async Task<IActionResult> Index(string statusFilter = "All", string priceSort = "")
        {
            try
            {
                var ordersQuery = _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .AsQueryable();

                // Filter by status
                if (statusFilter != "All")
                {
                    ordersQuery = ordersQuery.Where(o => o.Status == statusFilter);
                }

                // Apply sorting
                if (!string.IsNullOrEmpty(priceSort))
                {
                    ordersQuery = priceSort switch
                    {
                        "price_asc" => ordersQuery.OrderBy(o => o.Total),
                        "price_desc" => ordersQuery.OrderByDescending(o => o.Total),
                        _ => ordersQuery.OrderByDescending(o => o.OrderDate)
                    };
                }
                else
                {
                    ordersQuery = ordersQuery.OrderByDescending(o => o.OrderDate);
                }

                ViewBag.StatusFilter = statusFilter;
                ViewBag.PriceSort = priceSort;

                var orders = await ordersQuery.ToListAsync();
                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading orders");
                TempData["Error"] = "Error loading orders. Please try again.";
                return View(new List<Order>());
            }
        }

        // GET: Dashboard/Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: Dashboard/Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string status)
        {
            var existingOrder = await _context.Orders.FindAsync(id);
            if (existingOrder == null)
            {
                return NotFound();
            }

            existingOrder.Status = status;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Order updated!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Dashboard/Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Dashboard/Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order != null)
                {
                    _context.Orders.Remove(order);
                    await _context.SaveChangesAsync();

                    // LOG ORDER DELETION
                    await _loggingService.LogActionAsync(
                        "OrderDeleted",
                        $"Order #{order.Id} deleted for customer: {order.CustomerName}",
                        User.Identity?.Name,
                        order.CustomerName,
                        order.CustomerPhone
                    );

                    TempData["Success"] = $"Order #{order.Id} deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Order not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order {OrderId}", id);
                TempData["Error"] = "Error deleting order. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }
    }
}