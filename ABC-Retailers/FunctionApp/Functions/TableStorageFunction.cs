// FUNCTION 1: Store information into Azure Tables
using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ABCRetailersFunctions.Models;

namespace ABCRetailersFunctions.Functions
{
    public class TableStorageFunction
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _tableServiceClient;

        public TableStorageFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TableStorageFunction>();

            var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnection")
                ?? throw new InvalidOperationException("AzureStorageConnection not configured");

            _tableServiceClient = new TableServiceClient(connectionString);
        }

        [Function("StoreCustomer")]
        public async Task<HttpResponseData> StoreCustomer(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "customers")] HttpRequestData req)
        {
            _logger.LogInformation("StoreCustomer function triggered");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var customer = JsonSerializer.Deserialize<Customer>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (customer == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid customer data");
                    return badResponse;
                }

                // Generate RowKey if not provided
                if (string.IsNullOrEmpty(customer.RowKey))
                {
                    customer.RowKey = Guid.NewGuid().ToString();
                }

                var tableClient = _tableServiceClient.GetTableClient("Customers");
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.AddEntityAsync(customer);

                _logger.LogInformation($"Customer stored successfully: {customer.RowKey}");

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    customerId = customer.RowKey,
                    message = "Customer created successfully"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error storing customer: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("StoreProduct")]
        public async Task<HttpResponseData> StoreProduct(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "products")] HttpRequestData req)
        {
            _logger.LogInformation("StoreProduct function triggered");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var product = JsonSerializer.Deserialize<Product>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (product == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid product data");
                    return badResponse;
                }

                if (string.IsNullOrEmpty(product.RowKey))
                {
                    product.RowKey = Guid.NewGuid().ToString();
                }

                var tableClient = _tableServiceClient.GetTableClient("Products");
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.AddEntityAsync(product);

                _logger.LogInformation($"Product stored successfully: {product.RowKey}");

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    productId = product.RowKey,
                    message = "Product created successfully"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error storing product: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("StoreOrder")]
        public async Task<HttpResponseData> StoreOrder(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequestData req)
        {
            _logger.LogInformation("StoreOrder function triggered");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var order = JsonSerializer.Deserialize<Order>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (order == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid order data");
                    return badResponse;
                }

                if (string.IsNullOrEmpty(order.RowKey))
                {
                    order.RowKey = Guid.NewGuid().ToString();
                }

                var tableClient = _tableServiceClient.GetTableClient("Orders");
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.AddEntityAsync(order);

                _logger.LogInformation($"Order stored successfully: {order.RowKey}");

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    orderId = order.RowKey,
                    message = "Order created successfully"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error storing order: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}