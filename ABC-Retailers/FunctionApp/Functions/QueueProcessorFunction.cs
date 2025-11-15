// FUNCTION 3: Queue Processing for transaction information
using ABCRetailersFunctions.Models;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ABCRetailersFunctions.Functions
{
    public class QueueProcessorFunction
    {
        private readonly ILogger _logger;

        public QueueProcessorFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<QueueProcessorFunction>();
        }

        [Function("ProcessOrderNotifications")]
        public async Task ProcessOrderNotifications(
            [QueueTrigger("order-notifications", Connection = "AzureStorageConnection")] string queueMessage)
        {
            _logger.LogInformation($"Processing order notification: {queueMessage}");

            try
            {
                var notification = JsonSerializer.Deserialize<OrderNotificationMessage>(queueMessage);

                if (notification == null)
                {
                    _logger.LogWarning("Invalid order notification message");
                    return;
                }

                // Process the order notification
                // Examples: Send email, update dashboard, trigger workflows
                _logger.LogInformation($"Order notification processed - OrderId: {notification.OrderId}, " +
                    $"Customer: {notification.CustomerName}, " +
                    $"Product: {notification.ProductName}, " +
                    $"Status: {notification.Status}");

                // Here you could:
                // - Send email to customer
                // - Update external systems
                // - Trigger shipping workflows
                // - Generate reports

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing order notification: {ex.Message}");
                throw; // This will move the message to poison queue after retries
            }
        }

        [Function("ProcessStockUpdates")]
        public async Task ProcessStockUpdates(
            [QueueTrigger("stock-updates", Connection = "AzureStorageConnection")] string queueMessage)
        {
            _logger.LogInformation($"Processing stock update: {queueMessage}");

            try
            {
                var stockUpdate = JsonSerializer.Deserialize<StockUpdateMessage>(queueMessage);

                if (stockUpdate == null)
                {
                    _logger.LogWarning("Invalid stock update message");
                    return;
                }

                _logger.LogInformation($"Stock update processed - ProductId: {stockUpdate.ProductId}, " +
                    $"Product: {stockUpdate.ProductName}, " +
                    $"Previous: {stockUpdate.PreviousStock}, " +
                    $"New: {stockUpdate.NewStock}");

                // Check if stock is low
                if (stockUpdate.NewStock < 10)
                {
                    _logger.LogWarning($"LOW STOCK ALERT: {stockUpdate.ProductName} has only {stockUpdate.NewStock} units remaining!");
                    // Send alert to admin, trigger reorder, etc.
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing stock update: {ex.Message}");
                throw;
            }
        }

        [Function("SendToQueue")]
        public async Task<HttpResponseData> SendToQueue(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "queue/{queueName}")] HttpRequestData req,
            string queueName)
        {
            _logger.LogInformation($"SendToQueue triggered for queue: {queueName}");

            try
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnection")
                    ?? throw new InvalidOperationException("AzureStorageConnection not configured");

                var queueClient = new QueueClient(connectionString, queueName);
                await queueClient.CreateIfNotExistsAsync();

                var messageBody = await new StreamReader(req.Body).ReadToEndAsync();
                await queueClient.SendMessageAsync(messageBody);

                _logger.LogInformation($"Message sent to queue '{queueName}' successfully");

                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    message = $"Message sent to queue '{queueName}'"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending message to queue: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}