using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ABCRetailers.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ISqlDatabaseService _sqlService; // 1. INJECT SQL SERVICE
        private readonly ILogger<HomeController> _logger;

        // 2. UPDATE CONSTRUCTOR
        public HomeController(
            IAzureStorageService storageService,
            ISqlDatabaseService sqlService, // ADDED
            ILogger<HomeController> logger)
        {
            _storageService = storageService;
            _sqlService = sqlService; // ADDED
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // 3. GET PRODUCTS AND ORDERS FROM SQL
                var products = await _sqlService.GetAllProductsAsync();
                var customers = await _storageService.GetAllEntitiesAsync<Customer>();
                var orders = await _sqlService.GetAllOrdersAsync(); // 4. USE SQL SERVICE

                var viewModel = new HomeViewModel
                {
                    FeaturedProducts = products.Where(p => p.IsActive)
                        .OrderByDescending(p => p.DateAdded)
                        .Take(5)
                        .ToList(),
                    ProductCount = products.Count(p => p.IsActive),
                    CustomerCount = customers.Count(c => c.IsActive), // Assumes Customer model is still Table Storage
                    OrderCount = orders.Count
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading home page");
                return View(new HomeViewModel());
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}