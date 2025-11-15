namespace ABCRetailers.Constants
{
    public static class StorageConstants
    {
        // Partition Keys
        public const string CustomerPartitionKey = "Customer";
        public const string ProductPartitionKey = "Product";
        public const string OrderPartitionKey = "Order";

        // Container Names
        public const string ProductImagesContainer = "product-images";
        public const string PaymentProofsContainer = "payment-proofs";

        // Queue Names
        public const string OrderNotificationsQueue = "order-notifications";
        public const string StockUpdatesQueue = "stock-updates";

        // File Share Names
        public const string ContractsShare = "contracts";
        public const string PaymentsDirectory = "payments";
    }
}