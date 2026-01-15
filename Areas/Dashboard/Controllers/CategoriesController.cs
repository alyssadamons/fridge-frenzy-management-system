using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using E_Commerce.Areas.Dashboard.Models;

namespace E_Commerce.Areas.Dashboard.Controllers
{
    [Area("Dashboard")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILoggingService _loggingService;

        public CategoriesController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment, ILoggingService loggingService)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _loggingService = loggingService;
        }

        // GET: Dashboard/Categories
        public async Task<IActionResult> Index(string searchName, string searchDescription, string sortOrder)
        {
            ViewData["NameSortParam"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DescriptionSortParam"] = sortOrder == "description" ? "description_desc" : "description";
            ViewData["ProductCountSortParam"] = sortOrder == "products" ? "products_desc" : "products";

            ViewData["SearchName"] = searchName;
            ViewData["SearchDescription"] = searchDescription;

            var categoriesQuery = _context.Categories
                .Include(c => c.Products)
                .AsQueryable();

            // Apply search filters
            if (!string.IsNullOrEmpty(searchName))
            {
                categoriesQuery = categoriesQuery.Where(c => c.Name.Contains(searchName));
            }

            if (!string.IsNullOrEmpty(searchDescription))
            {
                categoriesQuery = categoriesQuery.Where(c => c.Description.Contains(searchDescription));
            }

            // Apply sorting
            categoriesQuery = sortOrder switch
            {
                "name_desc" => categoriesQuery.OrderByDescending(c => c.Name),
                "description" => categoriesQuery.OrderBy(c => c.Description),
                "description_desc" => categoriesQuery.OrderByDescending(c => c.Description),
                "products" => categoriesQuery.OrderBy(c => c.Products.Count),
                "products_desc" => categoriesQuery.OrderByDescending(c => c.Products.Count),
                _ => categoriesQuery.OrderBy(c => c.Name)
            };

            var categories = await categoriesQuery
                .Select(c => new CategoryViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Image = c.Image,
                    ProductCount = c.Products.Count
                })
                .ToListAsync();

            return View(categories);
        }

        // GET: Dashboard/Categories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Products)
                .Select(c => new CategoryViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Image = c.Image,
                    ProductCount = c.Products.Count
                })
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Dashboard/Categories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Dashboard/Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category, IFormFile Image)
        {
            if (ModelState.IsValid)
            {
                if (Image != null && Image.Length > 0)
                {
                    // Use the same SaveImageFile method as ProductsController
                    var filePath = await SaveCategoryImageFile(Image);
                    category.Image = filePath;
                }
                else
                {
                    ModelState.AddModelError("Image", "Image is required.");
                    return View(category);
                }

                _context.Add(category);
                await _context.SaveChangesAsync();

                // LOG CATEGORY CREATION
                await _loggingService.LogActionAsync(
                    "CategoryAdded",
                    $"New category added: {category.Name}",
                    User.Identity?.Name
                );

                TempData["Success"] = "Category created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Dashboard/Categories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // POST: Dashboard/Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormFile Image, Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
                    if (existingCategory == null)
                    {
                        return NotFound();
                    }

                    // Handle image upload if a new image is provided
                    if (Image != null && Image.Length > 0)
                    {
                        // Delete old image file if it exists
                        if (!string.IsNullOrEmpty(existingCategory.Image))
                        {
                            var oldImagePath = Path.Combine(_hostEnvironment.WebRootPath, existingCategory.Image.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                try
                                {
                                    System.IO.File.Delete(oldImagePath);
                                }
                                catch
                                {
                                    // Log error if needed, but don't fail the update
                                }
                            }
                        }

                        // Save new image using consistent method
                        var filePath = await SaveCategoryImageFile(Image);
                        existingCategory.Image = filePath;
                    }

                    // Update properties
                    existingCategory.Name = category.Name;
                    existingCategory.Description = category.Description;

                    _context.Update(existingCategory);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "CategoryUpdated",
                        $"Category updated: {category.Name}",
                        User.Identity?.Name
                    );

                    TempData["Success"] = "Category updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(category);
        }

        // GET: Dashboard/Categories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.Id == id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Dashboard/Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category != null)
                {
                    // If category has products, set their CategoryId to null before deleting
                    if (category.Products != null && category.Products.Any())
                    {
                        foreach (var product in category.Products)
                        {
                            product.CategoryId = null;
                        }
                    }

                    // Delete the image file if it exists
                    if (!string.IsNullOrEmpty(category.Image))
                    {
                        var imagePath = Path.Combine(_hostEnvironment.WebRootPath, category.Image.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            try
                            {
                                System.IO.File.Delete(imagePath);
                            }
                            catch
                            {
                                // Log error but continue with category deletion
                            }
                        }
                    }

                    // Hard delete the category
                    _context.Categories.Remove(category);
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(
                        "CategoryDeleted",
                        $"Category deleted: {category.Name}",
                        User.Identity?.Name
                    );

                    TempData["Success"] = "Category deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while deleting the category.";
                await _loggingService.LogActionAsync(
                    "CategoryDeleteError",
                    $"Error deleting category: {ex.Message}",
                    User.Identity?.Name
                );
                return RedirectToAction(nameof(Index));
            }
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }

        private async Task<string> SaveCategoryImageFile(IFormFile imageFile)
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

            // Create uploads folder for categories
            var uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "img", "categories");

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
            return $"/img/categories/{uniqueFileName}";
        }
    }
}