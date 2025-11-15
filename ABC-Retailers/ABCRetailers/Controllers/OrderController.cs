using ABCRetailers.Constants;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace ABCRetailers.Controllers
{
    public class OrderController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<OrderController> _logger;
        private readonly ISqlDatabaseService _sqlService;

        public OrderController(IAzureStorageService storageService, ISqlDatabaseService sqlService, ILogger<OrderController> logger)
        {
            _storageService = storageService;
            _sqlService = sqlService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                List<Order> orders;

                if (User.IsInRole("Admin")) // Example: Admins see all orders
                {
                    orders = await _sqlService.GetAllOrdersAsync();
                }
                else if (Guid.TryParse(userIdClaim, out var userId))
                {
                    orders = await _sqlService.GetOrdersByUserIdAsync(userId);
                }
                else
                {
                    orders = new List<Order>(); // Not logged in, show no orders
                }

                return View(orders.OrderByDescending(o => o.OrderDate).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading orders");
                TempData["Error"] = "Error loading orders. Please try again.";
                return View(new List<Order>());
            }
        }

        public async Task<IActionResult> Create()
        {
            // Gets Customers from Table Storage
            var customers = await _storageService.GetAllEntitiesAsync<Customer>();
            // Gets Products from SQL
            var products = await _sqlService.GetAllProductsAsync();

            var viewModel = new OrderCreateViewModel
            {
                Customers = customers.Where(c => c.IsActive).ToList(),
                Products = products.Where(p => p.IsActive && p.StockAvailable > 0).ToList()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Get Customer from Table Storage
                    var customer = await _storageService.GetEntityAsync<Customer>(
                        StorageConstants.CustomerPartitionKey, model.CustomerId);

                    // Get Product from SQL
                    var product = await _sqlService.GetProductByIdAsync(model.ProductId);

                    if (customer == null || product == null)
                    {
                        ModelState.AddModelError("", "Invalid customer or product selected.");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    if (product.StockAvailable < model.Quantity)
                    {
                        ModelState.AddModelError("Quantity", $"Insufficient stock. Available: {product.StockAvailable}");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    // Create the SQL Order object
                    var order = new Order
                    {
                        UserId = Guid.Empty, // This is a manual order, no logged-in user
                        CustomerName = customer.Username, // Use a real name property from Customer
                        CustomerEmail = customer.Email, // Use a real email property from Customer
                        OrderDate = DateTime.UtcNow,
                        Status = "Submitted",
                        Subtotal = (decimal)(product.Price * model.Quantity),
                        Tax = (decimal)(product.Price * model.Quantity) * 0.15m,
                        ShippingCost = (decimal)(product.Price * model.Quantity) > 100 ? 0 : 10.00m,
                        TotalAmount = ((decimal)(product.Price * model.Quantity) * 1.15m) + ((decimal)(product.Price * model.Quantity) > 100 ? 0 : 10.00m),

                        OrderItems = new List<OrderItem>
                        {
                            new OrderItem
                            {
                                ProductId = model.ProductId,
                                ProductName = product.ProductName,
                                Quantity = model.Quantity,
                                UnitPrice = (decimal)product.Price,
                                TotalPrice = (decimal)(product.Price * model.Quantity)
                            }
                        }
                    };

                    // Use SQL Service to create the order (handles transaction and stock)
                    await _sqlService.CreateOrderAsync(order);

                    // Send queue message (using Storage Service)
                    var orderMessage = new
                    {
                        OrderId = order.OrderId,
                        CustomerName = order.CustomerName,
                        ProductName = product.ProductName,
                        Quantity = model.Quantity,
                        TotalPrice = order.TotalAmount
                    };

                    await _storageService.SendMessageAsync(
                        StorageConstants.OrderNotificationsQueue,
                        JsonSerializer.Serialize(orderMessage));

                    TempData["Success"] = "Order created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating order");
                    ModelState.AddModelError("", $"Error creating order: {ex.Message}");
                }
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            if (id == Guid.Empty) return NotFound();

            var order = await _sqlService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();

            return View(order);
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            if (id == Guid.Empty) return NotFound();

            var order = await _sqlService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();

            if (order.Status == "Completed" || order.Status == "Cancelled")
            {
                TempData["Error"] = "Cannot edit completed or cancelled orders.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order order)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var originalOrder = await _sqlService.GetOrderByIdAsync(order.OrderId);
                    if (originalOrder == null) return NotFound();

                    originalOrder.Status = order.Status;
                    originalOrder.TrackingNumber = order.TrackingNumber;

                    if (order.Status == "Shipped" && !originalOrder.ShippedDate.HasValue)
                    {
                        originalOrder.ShippedDate = DateTime.UtcNow;
                    }

                    if (order.Status == "Delivered" && !originalOrder.DeliveredDate.HasValue)
                    {
                        originalOrder.DeliveredDate = DateTime.UtcNow;
                    }

                    await _sqlService.UpdateOrderAsync(originalOrder);
                    TempData["Success"] = "Order updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating order");
                    ModelState.AddModelError("", "Error updating order. Please try again.");
                }
            }
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid id) // Use Guid
        {
            try
            {
                var order = await _sqlService.GetOrderByIdAsync(id);
                if (order == null) return NotFound();

                if (order.Status == "Completed" || order.Status == "Cancelled")
                {
                    TempData["Error"] = "Cannot cancel completed or already cancelled orders.";
                    return RedirectToAction(nameof(Index));
                }

                // Restore stock for all items in the order
                foreach (var item in order.OrderItems)
                {
                    var product = await _sqlService.GetProductByIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockAvailable += item.Quantity;
                        await _sqlService.UpdateProductAsync(product);
                    }
                }

                order.Status = "Cancelled";
                await _sqlService.UpdateOrderAsync(order);

                TempData["Success"] = "Order cancelled successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order");
                TempData["Error"] = $"Error cancelling order: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<JsonResult> GetProductPrice(string productId)
        {
            try
            {
                var product = await _sqlService.GetProductByIdAsync(productId);

                if (product != null)
                {
                    return Json(new
                    {
                        success = true,
                        price = product.Price,
                        stock = product.StockAvailable,
                        productName = product.ProductName
                    });
                }
                return Json(new { success = false });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, string newStatus) // Use Guid
        {
            try
            {
                var order = await _sqlService.UpdateOrderStatusAsync(id, newStatus);
                if (order != null) {
               
                
                    return Json(new { success = false, message = "Order not found" });
                }

                // Send queue message for status update
                var statusMessage = new
                {
                    OrderId = order.OrderId,
                    CustomerName = order.CustomerName,
                    NewStatus = newStatus,
                    UpdatedDate = DateTime.UtcNow,
                };

                await _storageService.SendMessageAsync(
                    StorageConstants.OrderNotificationsQueue,
                    JsonSerializer.Serialize(statusMessage));

                return Json(new { success = true, message = $"Order status updated to {newStatus}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status");
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            model.Customers = await _storageService.GetAllEntitiesAsync<Customer>();
            model.Products = await _sqlService.GetAllProductsAsync();
        }
    }
}