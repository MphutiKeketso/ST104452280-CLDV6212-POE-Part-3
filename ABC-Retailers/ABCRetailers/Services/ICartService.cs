using ABCRetailers.Models;

namespace ABCRetailers.Services
{
    /// <summary>
    /// Service for managing the user's shopping cart,
    /// stored in the session.
    /// </summary>
    public interface ICartService
    {
        /// <summary>
        /// Gets the current user's cart from the session.
        /// </summary>
        Task<Cart> GetCartAsync();

        /// <summary>
        /// Adds a product to the cart and saves to session.
        /// </summary>
        /// <returns>True if added, false if out of stock.</returns>
        Task<bool> AddToCartAsync(string productId, int quantity);

        /// <summary>
        /// Updates an item's quantity in the cart and saves to session.
        /// </summary>
        /// <returns>True if updated, false if out of stock.</returns>
        Task<bool> UpdateQuantityAsync(string productId, int quantity);

        /// <summary>
        /// Removes an item from the cart and saves to session.
        /// </summary>
        Task RemoveFromCartAsync(string productId);

        /// <summary>
        /// Clears all items from the cart and saves to session.
        /// </summary>
        Task ClearCartAsync();

        /// <summary>
        /// Validates all cart items against database stock and prices.
        /// </summary>
        /// <returns>A list of friendly warning messages for the user.</returns>
        Task<List<string>> ValidateCartAsync();

        /// <summary>
        /// Creates an order from the cart, saves it to the database,
        /// and clears the cart.
        /// </summary>
        /// <returns>The created OrderDb object or null if failed.</returns>
        Task<Order?> CheckoutAsync(CheckoutViewModel model);
    }
}