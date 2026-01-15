using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace E_Commerce.Areas.Dashboard.Controllers
{
    [Area("Dashboard")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILoggingService _loggingService;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ProductsController(ApplicationDbContext context, ILoggingService loggingService, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _loggingService = loggingService;
            _hostEnvironment = hostEnvironment;
        }

        // GET: Dashboard/Products
        public async Task<IActionResult> Index(string searchName, int? categoryId, string sortOrder = "name_asc")
        {
            // Store filter values in ViewData for form persistence
            ViewData["SearchName"] = searchName;
            ViewData["CategoryId"] = categoryId;
            ViewData["SortOrder"] = sortOrder;

            // Get all categories for filter dropdown
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

            // Start with all products
            var products = _context.Products.Include(p => p.Category).AsQueryable();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchName))
            {
                products = products.Where(p => p.Name.Contains(searchName));
            }

            // Apply category filter
            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            // Apply sorting
            products = sortOrder switch
            {
                "name_desc" => products.OrderByDescending(p => p.Name),
                "price_asc" => products.OrderBy(p => p.Price),
                "price_desc" => products.OrderByDescending(p => p.Price),
                _ => products.OrderBy(p => p.Name) // default: name_asc
            };

            return View(await products.ToListAsync());
        }

        // GET: Dashboard/Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product == null)
                {
                    return NotFound();
                }

                return View(product);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "ProductDetailsError",
                    $"Error loading product details: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        // GET: Dashboard/Products/Create
        public IActionResult Create()
        {
            ViewBag.Categories = _context.Categories.ToList();
            return View();
        }

        // POST: Dashboard/Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile ImageFile)
        {
            Console.WriteLine("=== CREATE PRODUCT STARTED ===");

            if (ModelState.IsValid)
            {
                try
                {
                    Console.WriteLine("ModelState is valid");
                    Console.WriteLine($"Product Name: {product.Name}");
                    Console.WriteLine($"Product Price: {product.Price}");
                    Console.WriteLine($"Product CategoryId: {product.CategoryId}");
                    Console.WriteLine($"Product Description: {product.Description}");

                    // Handle file upload
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        Console.WriteLine($"ImageFile received - Name: {ImageFile.FileName}, Size: {ImageFile.Length}");

                        // Save the file and get the path/URL
                        var filePath = await SaveImageFile(ImageFile);
                        Console.WriteLine($"File saved successfully. Path: {filePath}");
                        product.Image = filePath;
                    }
                    else
                    {
                        Console.WriteLine("No ImageFile provided");
                        ModelState.AddModelError("ImageFile", "Please select an image file.");
                        ViewBag.Categories = _context.Categories.ToList();
                        return View(product);
                    }

                    Console.WriteLine("Attempting to add product to context...");
                    _context.Products.Add(product);

                    Console.WriteLine("Attempting to save changes to database...");
                    await _context.SaveChangesAsync();
                    Console.WriteLine("Database save successful!");

                    await _loggingService.LogActionAsync(
                        "ProductAdded",
                        $"New product added: {product.Name}",
                        User.Identity?.Name
                    );

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException dbEx)
                {
                    Console.WriteLine($"DB UPDATE EXCEPTION: {dbEx.Message}");
                    Console.WriteLine($"INNER EXCEPTION: {dbEx.InnerException?.Message}");
                    Console.WriteLine($"STACK TRACE: {dbEx.StackTrace}");

                    ModelState.AddModelError(string.Empty, $"Database error: {dbEx.InnerException?.Message ?? dbEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GENERAL EXCEPTION: {ex.Message}");
                    Console.WriteLine($"INNER EXCEPTION: {ex.InnerException?.Message}");
                    Console.WriteLine($"STACK TRACE: {ex.StackTrace}");

                    ModelState.AddModelError(string.Empty, $"Error creating product: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("ModelState is INVALID");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"ModelError: {error.ErrorMessage}");
                }
            }

            ViewBag.Categories = _context.Categories.ToList();
            return View(product);
        }
        // GET: Dashboard/Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product == null)
                {
                    return NotFound();
                }

                ViewBag.Categories = await _context.Categories.ToListAsync();
                return View(product);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "ProductEditError",
                    $"Error loading product for edit: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile ImageFile)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingProduct = await _context.Products.FindAsync(id);
                    if (existingProduct == null)
                    {
                        return NotFound();
                    }

                    // Handle file upload - only update if new file is provided
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        var filePath = await SaveImageFile(ImageFile);
                        existingProduct.Image = filePath;
                    }
                    // If no new file, keep the existing image

                    // Update other product details
                    existingProduct.Name = product.Name;
                    existingProduct.Description = product.Description;
                    existingProduct.Price = product.Price;
                    existingProduct.CategoryId = product.CategoryId;

                    _context.Update(existingProduct);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "ProductUpdated",
                        $"Product updated: {product.Name}",
                        User.Identity?.Name
                    );

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error updating product: {ex.Message}");
                }
            }
            ViewBag.Categories = _context.Categories.ToList();
            return View(product);
        }



        // GET: Dashboard/Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product == null)
                {
                    return NotFound();
                }

                return View(product);
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "DeleteProductError",
                    $"Error loading product for delete: {ex.Message}",
                    User.Identity?.Name
                );
                return NotFound();
            }
        }

        // POST: Dashboard/Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.FridgeRegistrations) // Include related fridge registrations
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product != null)
                {
                    // Set ProductId to NULL in all related FridgeRegistrations
                    if (product.FridgeRegistrations != null && product.FridgeRegistrations.Any())
                    {
                        foreach (var fridgeReg in product.FridgeRegistrations)
                        {
                            fridgeReg.ProductId = null;
                        }
                    }

                    // Now delete the product
                    _context.Products.Remove(product);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "ProductDeleted",
                        $"Product deleted: {product.Name}",
                        User.Identity?.Name
                    );

                    TempData["Success"] = "Product deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Product not found.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await _loggingService.LogActionAsync(
                    "DeleteProductError",
                    $"Error deleting product: {ex.Message}",
                    User.Identity?.Name
                );
                TempData["Error"] = $"An error occurred while deleting the product: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<string> SaveImageFile(IFormFile imageFile)
        {
            // Validate file
            if (imageFile == null || imageFile.Length == 0)
                throw new Exception("No file provided.");

            if (imageFile.Length > 5 * 1024 * 1024)
                throw new Exception("File size must be less than 5MB.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
                throw new Exception("Only image files are allowed (jpg, jpeg, png, gif, webp).");

            // Ensure wwwroot exists
            if (string.IsNullOrEmpty(_hostEnvironment.WebRootPath))
            {
                _hostEnvironment.WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            // Create uploads folder for products (consistent with categories)
            var uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "img", "products");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Generate unique filename
            var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Save file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            // Return consistent path (always forward slashes, no grp-03-07 prefix)
            return $"/img/products/{uniqueFileName}";
        }
    }
    }