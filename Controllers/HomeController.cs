using E_Commerce.Data;
using E_Commerce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace E_Commerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Contact()
        {
            return View();
        }



        // Products page - shows all available refrigerators
        public async Task<IActionResult> Products()
        {
            var products = await _context.Products.ToListAsync();
            return View(products);
        }

        // Maintenance page - for scheduling maintenance services
        public IActionResult Maintenance()
        {
            return View();
        }

        // Fault reporting page
        public IActionResult ReportFault()
        {
            return View();
        }

        // Customer account management - redirect to Identity manage page
        public IActionResult Account()
        {
            if (User.Identity.IsAuthenticated)
            {
                return LocalRedirect("~/Identity/Account/Manage");
            }
            return LocalRedirect("~/Identity/Account/Login");
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()    
        {
            return View(new ErrorViewModel
            {
                RequestId = int.TryParse(Activity.Current?.Id ?? HttpContext.TraceIdentifier, out int result) ? result : null
            });
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}