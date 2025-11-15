using ABCRetailers.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;  // For getting User ID at checkout
using System.Text.Json;        // For session serialization

namespace ABCRetailers.Services
{
    public class CartService : ICartService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISqlDatabaseService _sqlService;
        private readonly IAzureStorageService _storageService; // For image URLs
        private readonly ILogger<CartService> _logger;
        private const string CartSessionKey = "ShoppingCart";

        public CartService(
            IHttpContextAccessor httpContextAccessor,
            ISqlDatabaseService sqlService,
            IAzureStorageService storageService,
            ILogger<CartService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _sqlService = sqlService;
            _storageService = storageService;
            _logger = logger;
        }

        #region Session Helper Methods

        private ISession? Session => _httpContextAccessor.HttpContext?.Session;

        /// <summary>
        /// Gets the cart from the session or creates a new one.
        /// </summary>
        private Cart GetCartFromSession()
        {
            if (Session == null)
            {
                _logger.LogWarning("Session is null. Cannot retrieve cart.");
                return new Cart();
            }

            var jsonCart = Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(jsonCart))
            {
                return new Cart(); // Return a new, empty cart
            }

            try
            {
                // Deserialize the cart from JSON
                return JsonSerializer.Deserialize<Cart>(jsonCart) ?? new Cart();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize cart from session.");
                return new Cart(); // Return new cart on error
            }
        }

        /// <summary>
        /// Saves the cart to the session.
        /// </summary>
        private void SaveCartToSession(Cart cart)
        {
            if (Session == null)
            {
                _logger.LogWarning("Session is null. Cannot save cart.");
                return;
            }

            try
            {
                // Serialize the cart to JSON
                var jsonCart = JsonSerializer.Serialize(cart);
                Session.SetString(CartSessionKey, jsonCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize cart for session.");
            }
        }

        #endregion

        #region ICartService Implementation

        public async Task<Cart> GetCartAsync()
        {
            var cart = GetCartFromSession();

            // --- Enrich Cart with Fresh Image URLs ---
            // Your Cart.cs AddItem method copies the URL. If that URL is a
            // short-lived SAS token, it will expire. This code
            // refreshes the URLs every time the cart is loaded.

            // We assume ProductImageUrl stores the FILENAME (e.g., "my-product.jpg")
            // and we generate the full, new URL here.
            try
            {
                foreach (var item in cart.Items)
                {
                    if (!string.IsNullOrEmpty(item.ProductImageUrl))
                    {
                        // This uses the FILENAME stored in ProductImageUrl
                        // to get a fresh SAS token URL.
                        item.ProductImageUrl = await _storageService.GetFileUrlAsync("products-container", item.ProductImageUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh image URLs for cart.");
                // We can continue, the view will just show broken images.
            }

            return cart;
        }

        public async Task<bool> AddToCartAsync(string productId, int quantity)
        {
            // NOTE: This assumes 'GetProductByIdAsync' exists in your ISqlDatabaseService
            var product = await _sqlService.GetProductByIdAsync(productId);
            if (product == null)
            {
                _logger.LogWarning("Attempted to add non-existent product {ProductId}", productId);
                return false;
            }

            var cart = GetCartFromSession();

            // Check stock
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            int newQuantity = (existingItem?.Quantity ?? 0) + quantity;

            if (product.StockAvailable < newQuantity)
            {
                _logger.LogWarning("Insufficient stock for {ProductId}. Requested {Req}, Available {Avail}", productId, newQuantity, product.StockAvailable);
                return false;
            }

            // We must set product.ImageUrl to be the FILENAME, not the full URL.
            // The AddItem logic in Cart.cs copies this.
            // GetCartAsync will resolve it to the full URL.
            // We assume the 'ImageUrl' on the Product model is the filename.

            // Your Cart.cs AddItem logic handles adding/updating.
            cart.AddItem(product, quantity);

            SaveCartToSession(cart);
            return true;
        }

        public async Task<bool> UpdateQuantityAsync(string productId, int quantity)
        {
            var cart = GetCartFromSession();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                _logger.LogWarning("Attempted to update non-existent item {ProductId} in cart.", productId);
                return false;
            }

            if (quantity > 0)
            {
                // Check stock
                // NOTE: This assumes 'GetProductByIdAsync' exists
                var product = await _sqlService.GetProductByIdAsync(productId);
                if (product == null) return false; // Product was deleted

                if (product.StockAvailable < quantity)
                {
                    _logger.LogWarning("Insufficient stock for {ProductId} on update.", productId);
                    return false;
                }
            }

            // Your Cart.cs UpdateQuantity handles the logic (including removal if 0)
            cart.UpdateQuantity(productId, quantity);
            SaveCartToSession(cart);
            return true;
        }

        public Task RemoveFromCartAsync(string productId)
        {
            var cart = GetCartFromSession();
            cart.RemoveItem(productId);
            SaveCartToSession(cart);
            return Task.CompletedTask;
        }

        public Task ClearCartAsync()
        {
            var cart = GetCartFromSession();
            cart.Clear();
            SaveCartToSession(cart);
            return Task.CompletedTask;
        }

        public async Task<List<string>> ValidateCartAsync()
        {
            var errors = new List<string>();
            var cart = GetCartFromSession();
            bool cartWasUpdated = false;

            // Use .ToList() to create a copy, allowing us to modify the
            // original cart.Items list inside the loop (by removing items).
            foreach (var item in cart.Items.ToList())
            {
                // NOTE: This assumes 'GetProductByIdAsync' exists
                var product = await _sqlService.GetProductByIdAsync(item.ProductId);

                if (product == null)
                {
                    errors.Add($"'{item.ProductName}' is no longer available and has been removed from your cart.");
                    cart.RemoveItem(item.ProductId);
                    cartWasUpdated = true;
                    continue;
                }

                // Check for price changes
                if (product.Price != item.UnitPrice)
                {
                    errors.Add($"The price for '{item.ProductName}' has changed to {product.Price:C}.");
                    item.UnitPrice = (decimal)product.Price;
                    // Manually update TotalPrice (Cart.cs doesn't have a method for this)
                    item.TotalPrice = item.Quantity * item.UnitPrice;
                    cartWasUpdated = true;
                }

                // Check for stock changes
                if (product.StockAvailable < item.Quantity)
                {
                    if (product.StockAvailable == 0)
                    {
                        errors.Add($"'{item.ProductName}' is now out of stock and has been removed.");
                        cart.RemoveItem(item.ProductId);
                    }
                    else
                    {
                        errors.Add($"Only {product.StockAvailable} of '{item.ProductName}' are available. Your quantity has been updated.");
                        item.Quantity = product.StockAvailable;
                        item.TotalPrice = item.Quantity * item.UnitPrice; // Manually update
                    }
                    cartWasUpdated = true;
                }
            }

            if (cartWasUpdated)
            {
                SaveCartToSession(cart);
            }

            return errors;
        }

        public async Task<Order?> CheckoutAsync(CheckoutViewModel model)
        {
            var cart = GetCartFromSession();
            if (!cart.Items.Any())
            {
                _logger.LogWarning("Checkout failed: Cart is empty.");
                return null;
            }

            // Get current User ID from claims
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogError("Checkout failed: User ID not found in claims or invalid.");
                // Allow guest checkout? For now, fail.
                return null;
            }

            // 1. Create the OrderDb object
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                Status = "Pending", // Initial status
                CustomerName = model.CustomerName,
                CustomerEmail = model.CustomerEmail,
                ShippingAddress = model.ShippingAddress,
                PhoneNumber = model.PhoneNumber,

                // Use the cart's final calculated totals
                Subtotal = cart.Subtotal,
                Tax = cart.Tax,
                ShippingCost = cart.ShippingCost,
                TotalAmount = cart.Total,
                OrderItems = new List<OrderItem>() // Use the correct list type
            };

            // 2. Create OrderItem objects from cart items
            foreach (var item in cart.Items)
            {
                // ADD OrderItem, NOT Order
                order.OrderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    // Store the FILENAME, not the full URL
                    ProductImageUrl = item.ProductImageUrl?.Contains("?") == true ?
                        new Uri(item.ProductImageUrl).AbsolutePath.Split('/').Last() :
                        item.ProductImageUrl,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                });
            }

            // 3. Save order to database
            try
            {
                // Your SqlDatabaseService.CreateOrderAsync now handles the transaction
                // for saving the order, its items, AND updating stock.
                var createdOrder = await _sqlService.CreateOrderAsync(order);

                // 4. Clear the cart
                await ClearCartAsync();

                return createdOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout failed during order creation for user {UserId}", userId);
                return null;
            }
        }

        #endregion
    }
}