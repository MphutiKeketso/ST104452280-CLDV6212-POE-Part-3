using Azure;
using Azure.Data.Tables;

namespace ABCRetailersFunctions.Models
{
    public class Order : ITableEntity
    {
        public string PartitionKey { get; set; } = "Order";
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string CustomerId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public int Quantity { get; set; }
        public int UnitPriceCents { get; set; }
        public int TotalPriceCents { get; set; }
        public string Status { get; set; } = "Submitted";
    }
}