using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Services;
using ABCRetailers.Constants; // Keep this for your container name

namespace ABCRetailers.Controllers
{
    public class ProductController : Controller
    {
        // --- INJECT SQL SERVICE ---
        private readonly ISqlDatabaseService _sqlService;
        private readonly IAzureStorageService _storageService; // Keep for images
        private readonly ILogger<ProductController> _logger;

        public ProductController(
            ISqlDatabaseService sqlService, // ADDED
            IAzureStorageService storageService,
            ILogger<ProductController> logger)
        {
            _sqlService = sqlService; // ADDED
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // --- Use SQL Service ---
                var products = await _sqlService.GetAllProductsAsync();
                return View(products.Where(p => p.IsActive).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                TempData["Error"] = "Error loading products. Please try again.";
                return View(new List<Product>());
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Upload image if provided (using Storage Service)
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var imageUrl = await _storageService.UploadImageAsync(imageFile, StorageConstants.ProductImagesContainer);
                        product.ImageUrl = imageUrl;
                    }

                    // 2. Save product to SQL (using SQL Service)
                    await _sqlService.CreateProductAsync(product);

                    TempData["Success"] = $"Product '{product.ProductName}' created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating product");
                    ModelState.AddModelError("", $"Error creating product: {ex.Message}");
                }
            }

            return View(product);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            // --- Use SQL Service ---
            var product = await _sqlService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Get original product
                    var originalProduct = await _sqlService.GetProductByIdAsync(product.ProductId);
                    if (originalProduct == null)
                    {
                        return NotFound();
                    }

                    // 2. Update properties
                    originalProduct.ProductName = product.ProductName;
                    originalProduct.Description = product.Description;
                    originalProduct.Price = product.Price;
                    originalProduct.StockAvailable = product.StockAvailable;
                    originalProduct.Category = product.Category;

                    // 3. Upload new image if provided (using Storage Service)
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var imageUrl = await _storageService.UploadImageAsync(imageFile, StorageConstants.ProductImagesContainer);
                        originalProduct.ImageUrl = imageUrl;
                    }

                    // 4. Save updates to SQL (using SQL Service)
                    await _sqlService.UpdateProductAsync(originalProduct);

                    TempData["Success"] = "Product updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating product");
                    ModelState.AddModelError("", "Error updating product. Please try again.");
                }
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                // --- Use SQL Service ---
                await _sqlService.DeleteProductAsync(id); // Performs soft delete
                TempData["Success"] = "Product deleted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product");
                TempData["Error"] = $"Error deleting product: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}