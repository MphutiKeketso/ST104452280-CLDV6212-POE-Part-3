using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Services;
using ABCRetailers.Constants;

namespace ABCRetailers.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(IAzureStorageService storageService, ILogger<CustomerController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var customers = await _storageService.GetAllEntitiesAsync<Customer>();
                return View(customers.Where(c => c.IsActive).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customers");
                TempData["Error"] = "Error loading customers. Please try again.";
                return View(new List<Customer>());
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // FIXED: Set RowKey explicitly
                    customer.RowKey = Guid.NewGuid().ToString();
                    customer.PartitionKey = StorageConstants.CustomerPartitionKey;
                    customer.DateRegistered = DateTime.UtcNow;
                    customer.IsActive = true;

                    await _storageService.AddEntityAsync(customer);
                    TempData["Success"] = "Customer created successfully";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating customer");
                    ModelState.AddModelError("", $"Error creating customer: {ex.Message}");
                }
            }
            return View(customer);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var customer = await _storageService.GetEntityAsync<Customer>(StorageConstants.CustomerPartitionKey, id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var originalCustomer = await _storageService.GetEntityAsync<Customer>(
                        StorageConstants.CustomerPartitionKey, customer.RowKey);

                    if (originalCustomer == null)
                    {
                        return NotFound();
                    }

                    // Update fields
                    originalCustomer.Name = customer.Name;
                    originalCustomer.Surname = customer.Surname;
                    originalCustomer.Email = customer.Email;
                    originalCustomer.Username = customer.Username;
                    originalCustomer.ShippingAddress = customer.ShippingAddress;
                    originalCustomer.PhoneNumber = customer.PhoneNumber;

                    await _storageService.UpdateEntityAsync(originalCustomer);
                    TempData["Success"] = "Customer updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating customer");
                    ModelState.AddModelError("", "Error updating customer. Please try again.");
                }
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                // FIXED: Soft delete instead of hard delete
                var customer = await _storageService.GetEntityAsync<Customer>(StorageConstants.CustomerPartitionKey, id);
                if (customer != null)
                {
                    customer.IsActive = false;
                    await _storageService.UpdateEntityAsync(customer);
                    TempData["Success"] = "Customer deleted successfully";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer");
                TempData["Error"] = $"Error deleting customer: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}