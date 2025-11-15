using ABCRetailers.Models;
using Microsoft.Data.SqlClient;
using Dapper;

namespace ABCRetailers.Services
{
    public class SqlDatabaseService : ISqlDatabaseService
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlDatabaseService> _logger;

        public SqlDatabaseService(IConfiguration configuration, ILogger<SqlDatabaseService> logger)
        {
            _connectionString = configuration.GetConnectionString("AzureSqlDatabase")
                ?? throw new InvalidOperationException("SQL connection string not found");
            _logger = logger;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        #region User Operations

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT * FROM Users WHERE Email = @Email AND IsActive = 1";
                return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}", email);
                throw;
            }
        }

        public async Task<User?> GetUserByIdAsync(Guid userId)
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT * FROM Users WHERE UserId = @UserId";
                return await connection.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT * FROM Users WHERE Username = @Username AND IsActive = 1";
                return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by username: {Username}", username);
                throw;
            }
        }

        public async Task<User> CreateUserAsync(User user)
        {
            try
            {
                using var connection = GetConnection();
                var sql = @"
                    INSERT INTO Users (UserId, Username, Email, PasswordHash, Salt, FirstName, LastName, 
                                      PhoneNumber, Address, Role, IsActive, DateRegistered, CreatedAt, UpdatedAt)
                    VALUES (@UserId, @Username, @Email, @PasswordHash, @Salt, @FirstName, @LastName,
                           @PhoneNumber, @Address, @Role, @IsActive, @DateRegistered, @CreatedAt, @UpdatedAt);
                    SELECT * FROM Users WHERE UserId = @UserId";

                user.UserId = Guid.NewGuid();
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                user.DateRegistered = DateTime.UtcNow;

                var result = await connection.QueryFirstAsync<User>(sql, user);
                _logger.LogInformation("User created: {Email}", user.Email);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Email}", user.Email);
                throw;
            }
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            try
            {
                using var connection = GetConnection();
                var sql = @"
                    UPDATE Users 
                    SET Username = @Username, Email = @Email, FirstName = @FirstName, LastName = @LastName,
                        PhoneNumber = @PhoneNumber, Address = @Address, Role = @Role, IsActive = @IsActive,
                        UpdatedAt = @UpdatedAt
                    WHERE UserId = @UserId;
                    SELECT * FROM Users WHERE UserId = @UserId";

                user.UpdatedAt = DateTime.UtcNow;
                var result = await connection.QueryFirstAsync<User>(sql, user);
                _logger.LogInformation("User updated: {UserId}", user.UserId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", user.UserId);
                throw;
            }
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
                var count = await connection.ExecuteScalarAsync<int>(sql, new { Email = email });
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email existence: {Email}", email);
                throw;
            }
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT COUNT(1) FROM Users WHERE Username = @Username";
                var count = await connection.ExecuteScalarAsync<int>(sql, new { Username = username });
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking username existence: {Username}", username);
                throw;
            }
        }

        public async Task UpdateLastLoginAsync(Guid userId)
        {
            try
            {
                using var connection = GetConnection();
                var sql = "UPDATE Users SET LastLogin = @LastLogin WHERE UserId = @UserId";
                await connection.ExecuteAsync(sql, new { UserId = userId, LastLogin = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last login: {UserId}", userId);
                throw;
            }
        }

        #endregion

        #region Order Operations

        public async Task<string> GenerateOrderNumberAsync()
        {
            // Generate order number: ORD-YYYYMMDD-XXXX
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            using var connection = GetConnection();
            var sql = "SELECT COUNT(*) FROM Orders WHERE OrderNumber LIKE @Pattern";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { Pattern = $"ORD-{date}%" });
            return $"ORD-{date}-{(count + 1):D4}";
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            try
            {
                using var connection = GetConnection();
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // 1. Insert the main Order
                    var orderSql = @"
                        INSERT INTO Orders (OrderId, UserId, OrderNumber, OrderDate, Status, CustomerName, 
                                          CustomerEmail, ShippingAddress, PhoneNumber, Subtotal, Tax, 
                                          ShippingCost, TotalAmount, CreatedAt, UpdatedAt)
                        VALUES (@OrderId, @UserId, @OrderNumber, @OrderDate, @Status, @CustomerName,
                               @CustomerEmail, @ShippingAddress, @PhoneNumber, @Subtotal, @Tax,
                               @ShippingCost, @TotalAmount, @CreatedAt, @UpdatedAt);
                        SELECT * FROM Orders WHERE OrderId = @OrderId";

                    order.OrderId = Guid.NewGuid();
                    order.OrderNumber = await GenerateOrderNumberAsync();
                    order.CreatedAt = DateTime.UtcNow;
                    order.UpdatedAt = DateTime.UtcNow;

                    var result = await connection.QueryFirstAsync<Order>(orderSql, order, transaction);

                    // 2. Insert all OrderItems
                    foreach (var item in order.OrderItems)
                    {
                        item.OrderId = order.OrderId; // Link item to the order
                        await CreateOrderItemAsync(item, connection, transaction);
                    }

                    // 3. TODO: Update product stock
                    // You must add this logic here, inside the transaction
                    foreach (var item in order.OrderItems)
                    {
                        var stockSql = "UPDATE Products SET StockAvailable = StockAvailable - @Quantity WHERE ProductId = @ProductId";
                        await connection.ExecuteAsync(stockSql, new { item.Quantity, item.ProductId }, transaction);
                    }

                    transaction.Commit();
                    _logger.LogInformation("Order created: {OrderNumber}", order.OrderNumber);
                    return result;
                }
                catch
                {
                    transaction.Rollback();
                    _logger.LogError("Transaction rolled back for order creation.");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                throw;
            }
        }

        private async Task CreateOrderItemAsync(OrderItem item, SqlConnection connection, SqlTransaction? transaction)
        {
            var sql = @"
                INSERT INTO OrderItems (OrderItemId, OrderId, ProductId, ProductName, ProductImageUrl,
                                       Quantity, UnitPrice, TotalPrice, CreatedAt)
                VALUES (@OrderItemId, @OrderId, @ProductId, @ProductName, @ProductImageUrl,
                       @Quantity, @UnitPrice, @TotalPrice, @CreatedAt)";

            item.OrderItemId = Guid.NewGuid();
            item.CreatedAt = DateTime.UtcNow;

            await connection.ExecuteAsync(sql, item, transaction);
        }

        public async Task CreateOrderItemAsync(OrderItem item)
        {
            try
            {
                using var connection = GetConnection();
                // This re-uses the private method, but without a transaction
                await CreateOrderItemAsync(item, connection, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order item");
                throw;
            }
        }

        public async Task<Order?> GetOrderByIdAsync(Guid orderId)
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT * FROM Orders WHERE OrderId = @OrderId";
                var order = await connection.QueryFirstOrDefaultAsync<Order>(sql, new { OrderId = orderId });

                if (order != null)
                {
                    order.OrderItems = await GetOrderItemsByOrderIdAsync(orderId);
                }

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order by ID: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<List<Order>> GetOrdersByUserIdAsync(Guid userId)
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT * FROM Orders WHERE UserId = @UserId ORDER BY OrderDate DESC";
                var orders = (await connection.QueryAsync<Order>(sql, new { UserId = userId })).ToList();

                foreach (var order in orders)
                {
                    order.OrderItems = await GetOrderItemsByOrderIdAsync(order.OrderId);
                }

                return orders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by user ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<List<Order>> GetAllOrdersAsync()
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT * FROM Orders ORDER BY OrderDate DESC";
                var orders = (await connection.QueryAsync<Order>(sql)).ToList();

                foreach (var order in orders)
                {
                    order.OrderItems = await GetOrderItemsByOrderIdAsync(order.OrderId);
                }

                return orders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all orders");
                throw;
            }
        }

        public async Task<Order> UpdateOrderAsync(Order order)
        {
            try
            {
                using var connection = GetConnection();
                var sql = @"
                    UPDATE Orders 
                    SET Status = @Status, TrackingNumber = @TrackingNumber, 
                        ShippedDate = @ShippedDate, DeliveredDate = @DeliveredDate, UpdatedAt = @UpdatedAt
                    WHERE OrderId = @OrderId;
                    SELECT * FROM Orders WHERE OrderId = @OrderId";

                order.UpdatedAt = DateTime.UtcNow;
                var result = await connection.QueryFirstAsync<Order>(sql, order);
                _logger.LogInformation("Order updated: {OrderId}", order.OrderId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order: {OrderId}", order.OrderId);
                throw;
            }
        }

        public async Task<Order> UpdateOrderStatusAsync(Guid orderId, string newStatus)
        {
            try
            {
                using var connection = GetConnection();
                var sql = @"
                    UPDATE Orders 
                    SET Status = @Status, 
                        ShippedDate = CASE WHEN @Status = 'Shipped' AND ShippedDate IS NULL THEN GETDATE() ELSE ShippedDate END,
                        DeliveredDate = CASE WHEN @Status = 'Delivered' AND DeliveredDate IS NULL THEN GETDATE() ELSE DeliveredDate END,
                        UpdatedAt = GETDATE()
                    WHERE OrderId = @OrderId;
                    SELECT * FROM Orders WHERE OrderId = @OrderId";

                var result = await connection.QueryFirstAsync<Order>(sql, new { OrderId = orderId, Status = newStatus });
                _logger.LogInformation("Order status updated: {OrderId} -> {Status}", orderId, newStatus);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<List<OrderItem>> GetOrderItemsByOrderIdAsync(Guid orderId)
        {
            try
            {
                using var connection = GetConnection();
                // FIX: Query the 'OrderItems' table and map to the 'OrderItem' model
                var sql = "SELECT * FROM OrderItems WHERE OrderId = @OrderId";
                var items = await connection.QueryAsync<OrderItem>(sql, new { OrderId = orderId });
                return items.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order items: {OrderId}", orderId);
                throw;
            }
        }




        public async Task<Product?> GetProductByIdAsync(string productId)
        {
            try
            {
                using var connection = GetConnection();
                // Assumes your products table is named "Products"
                var sql = "SELECT * FROM Products WHERE ProductId = @ProductId";
                return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { ProductId = productId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product by ID: {ProductId}", productId);
                throw;
            }
        }

        public async Task<List<Product>> GetProductsByIdsAsync(List<string> productIds)
        {
            try
            {
                using var connection = GetConnection();
                // Assumes your products table is named "Products"
                var sql = "SELECT * FROM Products WHERE ProductId IN @ProductIds";
                var products = await connection.QueryAsync<Product>(sql, new { ProductIds = productIds });
                return products.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by IDs");
                throw;
            }
        }

        #region Product Operations

        public async Task<List<Product>> GetAllProductsAsync()
        {
            try
            {
                using var connection = GetConnection();
                var sql = "SELECT * FROM Products";
                var products = await connection.QueryAsync<Product>(sql);
                return products.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all products");
                throw;
            }
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            try
            {
                using var connection = GetConnection();
                var sql = @"
                    INSERT INTO Products (ProductId, ProductName, Description, Price, 
                                          StockAvailable, ImageUrl, IsActive, DateAdded)
                    VALUES (@ProductId, @ProductName, @Description, @Price, 
                           @StockAvailable, @ImageUrl, @IsActive, @DateAdded);
                    SELECT * FROM Products WHERE ProductId = @ProductId";

                // Set new ID and timestamps
                product.ProductId = Guid.NewGuid().ToString();
                product.IsActive = true;
                product.DateAdded = DateTime.UtcNow;

                var result = await connection.QueryFirstAsync<Product>(sql, product);
                _logger.LogInformation("Product created: {ProductId}", product.ProductId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product: {ProductName}", product.ProductName);
                throw;
            }
        }

        public async Task<Product> UpdateProductAsync(Product product)
        {
            try
            {
                using var connection = GetConnection();
                var sql = @"
                    UPDATE Products 
                    SET ProductName = @ProductName, 
                        Description = @Description, 
                        Price = @Price, 
                        StockAvailable = @StockAvailable, 
                        ImageUrl = @ImageUrl, 
                        IsActive = @IsActive
                    WHERE ProductId = @ProductId;
                    SELECT * FROM Products WHERE ProductId = @ProductId";

                var result = await connection.QueryFirstAsync<Product>(sql, product);
                _logger.LogInformation("Product updated: {ProductId}", product.ProductId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product: {ProductId}", product.ProductId);
                throw;
            }
        }

        public async Task DeleteProductAsync(string productId)
        {
            try
            {
                using var connection = GetConnection();
                // We perform a "soft delete" by setting IsActive to false
                var sql = "UPDATE Products SET IsActive = 0 WHERE ProductId = @ProductId";
                await connection.ExecuteAsync(sql, new { ProductId = productId });
                _logger.LogInformation("Product (soft) deleted: {ProductId}", productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product: {ProductId}", productId);
                throw;
            }
        }

        #endregion


        #endregion
    }
}