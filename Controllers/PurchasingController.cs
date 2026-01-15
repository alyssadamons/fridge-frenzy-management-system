using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Commerce.Controllers
{
    public class PurchasingController : Controller
    {
        private readonly ILogger<PurchasingController> _logger;
        private ApplicationDbContext _context;

        public PurchasingController(ILogger<PurchasingController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(int? categoryid, string sortBy = "name", string priceRange = "all", string search = "")
        {
            try
            {
                // 1. Start with all products (including category reference)
                var productsQuery = _context.Products.Include(x => x.Category).AsQueryable();

                // 2. Load all categories (needed for the top navigation bar)
                var categories = await _context.Categories.ToListAsync();

                // 3. Apply search filter if present
                if (!string.IsNullOrEmpty(search))
                {
                    productsQuery = productsQuery.Where(x => x.Name.Contains(search));
                }

                // 4. Apply category filter if present - ONLY when categoryid has value
                if (categoryid.HasValue && categoryid.Value > 0)
                {
                    productsQuery = productsQuery.Where(x => x.CategoryId == categoryid.Value);
                }

                // 5. Apply price range filter
                if (!string.IsNullOrEmpty(priceRange) && priceRange != "all")
                {
                    switch (priceRange)
                    {
                        case "under10000":
                            productsQuery = productsQuery.Where(x => x.Price < 10000);
                            break;
                        case "10000-50000":
                            productsQuery = productsQuery.Where(x => x.Price >= 10000 && x.Price <= 50000);
                            break;
                        case "50000-100000":
                            productsQuery = productsQuery.Where(x => x.Price > 50000 && x.Price <= 100000);
                            break;
                        case "over100000":
                            productsQuery = productsQuery.Where(x => x.Price > 100000);
                            break;
                    }
                }

                // 6. Apply sorting
                switch (sortBy)
                {
                    case "price_low":
                        productsQuery = productsQuery.OrderBy(x => x.Price);
                        break;
                    case "price_high":
                        productsQuery = productsQuery.OrderByDescending(x => x.Price);
                        break;
                    case "name":
                    default:
                        productsQuery = productsQuery.OrderBy(x => x.Name);
                        break;
                }

                // 7. Execute the query to get the filtered products
                var products = await productsQuery.ToListAsync();

                // 8. Create and return the ProductViewModel with all necessary data
                var model = new ProductViewModel
                {
                    Products = products,
                    Categories = categories,
                    SelectedCategoryId = categoryid,
                    SortBy = sortBy,
                    PriceRange = priceRange
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                // Return empty model on error
                return View(new ProductViewModel
                {
                    Categories = await _context.Categories.ToListAsync()
                });
            }
        }

        // Added Details action for product details page
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(x => x.Category)
                    .FirstOrDefaultAsync(x => x.ProductId == id);

                if (product == null)
                {
                    return NotFound();
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product details for ID {ProductId}", id);
                return NotFound();
            }
        }

        // FIXED: Correct action name for the partial view
        public async Task<IActionResult> GetProductDetailsPartial(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(x => x.Category)
                    .FirstOrDefaultAsync(x => x.ProductId == id);

                if (product == null)
                {
                    return NotFound();
                }

                return PartialView("_ProductDetailsPartial", product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product details for ID {ProductId}", id);
                return Content("<div class='alert alert-danger'>Error loading product details. Please try again.</div>");
            }
        }
        // Add this to PurchasingController for immediate debugging
        public async Task<IActionResult> CheckImagePaths()
        {
            var products = await _context.Products.ToListAsync();
            var result = new System.Text.StringBuilder();

            result.AppendLine("<h1>Image Path Debug</h1>");

            foreach (var product in products)
            {
                result.AppendLine($"<h3>Product: {product.Name} (ID: {product.ProductId})</h3>");
                result.AppendLine($"<p>Database Image Path: <strong>{product.Image}</strong></p>");

                if (!string.IsNullOrEmpty(product.Image))
                {
                    var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.Image.TrimStart('/'));
                    var exists = System.IO.File.Exists(physicalPath);
                    result.AppendLine($"<p>Physical Path: {physicalPath}</p>");
                    result.AppendLine($"<p>File Exists: <span style='color: {(exists ? "green" : "red")}'>{exists}</span></p>");

                    if (exists)
                    {
                        result.AppendLine($"<p>✅ File found at: {physicalPath}</p>");
                    }
                    else
                    {
                        result.AppendLine($"<p>❌ File NOT found at: {physicalPath}</p>");
                    }
                }
                else
                {
                    result.AppendLine("<p>❌ No image path in database</p>");
                }
                result.AppendLine("<hr>");
            }

            return Content(result.ToString(), "text/html");
        }
    }
}