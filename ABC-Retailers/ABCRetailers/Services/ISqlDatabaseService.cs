using ABCRetailers.Models;

namespace ABCRetailers.Services
{
    public interface ISqlDatabaseService
    {
        // User operations
        Task<User?> GetUserByEmailAsync(string email); 
        Task<User?> GetUserByIdAsync(Guid userId); 
        Task<User?> GetUserByUsernameAsync(string username); 
        Task<User> CreateUserAsync(User user); 
        Task<User> UpdateUserAsync(User user); 
        Task<bool> EmailExistsAsync(string email); 
        Task<bool> UsernameExistsAsync(string username); 
        Task UpdateLastLoginAsync(Guid userId); 

        // Order operations
        Task<Order> CreateOrderAsync(Order order); 
        Task<Order?> GetOrderByIdAsync(Guid orderId); 
        Task<List<Order>> GetOrdersByUserIdAsync(Guid userId); 
        Task<List<Order>> GetAllOrdersAsync(); 
        Task<Order> UpdateOrderAsync(Order order); 
        Task<Order> UpdateOrderStatusAsync(Guid orderId, string newStatus); 
        Task<string> GenerateOrderNumberAsync();

        // Order Items operations
        
        Task CreateOrderItemAsync(OrderItem item); // <-- FIX: Use OrderItem model
        Task<List<OrderItem>> GetOrderItemsByOrderIdAsync(Guid orderId); // <-- FIX: Return List<OrderItem>

        // [ADDED] Product operations
        Task<Product?> GetProductByIdAsync(string productId);
        Task<List<Product>> GetProductsByIdsAsync(List<string> productIds);
        Task<List<Product>> GetAllProductsAsync();
        Task<Product> CreateProductAsync(Product product);
        Task<Product> UpdateProductAsync(Product product);
        Task DeleteProductAsync(string productId); // For soft delete
    }
}