using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetailers.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly IAzureStorageService _storageService;
        private readonly ISqlDatabaseService _sqlService;
        private readonly ILogger<CartController> _logger;

        public CartController(
            ICartService cartService,
            IAzureStorageService storageService,
            ISqlDatabaseService sqlService,
            ILogger<CartController> logger)
        {
            _cartService = cartService;
            _storageService = storageService;
            _sqlService = sqlService;
            _logger = logger;
        }

        // GET: Cart/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var cart = await _cartService.GetCartAsync();
                var validationErrors = await _cartService.ValidateCartAsync();

                if (validationErrors.Any())
                {
                    foreach (var error in validationErrors)
                    {
                        TempData["Warning"] = error;
                    }
                }

                return View(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cart");
                TempData["Error"] = "Error loading cart. Please try again.";
                return View(new Cart());
            }
        }

        // POST: Cart/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(string productId, int quantity = 1)
        {
            try
            {
                var success = await _cartService.AddToCartAsync(productId, quantity);

                if (success)
                {
                    TempData["Success"] = "Product added to cart successfully!";
                }
                else
                {
                    TempData["Error"] = "Unable to add product to cart. Please check stock availability.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding product {productId} to cart");
                TempData["Error"] = "An error occurred while adding the product to cart.";
            }

            // Redirect back to referring page or products page
            var referrer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referrer))
            {
                return Redirect(referrer);
            }

            return RedirectToAction("Index", "Product");
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(string productId, int quantity)
        {
            try
            {
                var success = await _cartService.UpdateQuantityAsync(productId, quantity);

                if (success)
                {
                    return Json(new { success = true, message = "Cart updated successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Unable to update cart. Please check stock availability." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating quantity for product {productId}");
                return Json(new { success = false, message = "An error occurred while updating the cart." });
            }
        }

        // POST: Cart/RemoveFromCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(string productId)
        {
            try
            {
                await _cartService.RemoveFromCartAsync(productId);
                TempData["Success"] = "Product removed from cart.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing product {productId} from cart");
                TempData["Error"] = "An error occurred while removing the product.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/ClearCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                await _cartService.ClearCartAsync();
                TempData["Success"] = "Cart cleared successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                TempData["Error"] = "An error occurred while clearing the cart.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Cart/Checkout
        public async Task<IActionResult> Checkout()
        {
            try
            {
                var cart = await _cartService.GetCartAsync();

                if (!cart.Items.Any())
                {
                    TempData["Error"] = "Your cart is empty. Please add items before checkout.";
                    return RedirectToAction("Index", "Product");
                }

                var validationErrors = await _cartService.ValidateCartAsync();
                if (validationErrors.Any())
                {
                    foreach (var error in validationErrors)
                    {
                        TempData["Error"] = error;
                    }
                    return RedirectToAction(nameof(Index));
                }

                var viewModel = new CheckoutViewModel
                {
                    Cart = cart
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading checkout page");
                TempData["Error"] = "Error loading checkout. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Cart/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            try
            {
                // Reload cart from session
                model.Cart = await _cartService.GetCartAsync();

                if (!model.Cart.Items.Any())
                {
                    TempData["Error"] = "Your cart is empty.";
                    return RedirectToAction("Index", "Product");
                }

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // Process checkout
                var order = await _cartService.CheckoutAsync(model);

                if (order == null)
                {
                    TempData["Error"] = "Unable to process your order. Please try again.";
                    return View(model);
                }

                TempData["Success"] = $"Order placed successfully! Your order number is {order.OrderNumber}";
                return RedirectToAction("Confirmation", new { orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing checkout");
                ModelState.AddModelError("", "An error occurred while processing your order. Please try again.");
                return View(model);
            }
        }

        // GET: Cart/Confirmation
        public async Task<IActionResult> Confirmation(Guid orderId)
        {
            try
            {
                // Note: You'll need to inject ISqlDatabaseService separately or add it to CartService
                // For now, redirect to Order/Details
                return RedirectToAction("Details", "Order", new { id = orderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading order confirmation for {orderId}");
                TempData["Error"] = "Error loading order details.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Cart/GetCartCount (AJAX endpoint for cart badge)
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            try
            {
                var cart = await _cartService.GetCartAsync();
                return Json(new { count = cart.TotalItems });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart count");
                return Json(new { count = 0 });
            }
        }
    }
}