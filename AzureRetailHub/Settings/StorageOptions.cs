namespace AzureRetailHub.Settings
{
    public class StorageOptions
    {
        public string ConnectionString { get; set; } = "";
        public string CustomersTable { get; set; } = "customers";
        public string ProductsTable { get; set; } = "products";
        public string OrdersTable { get; set; } = "orders";
        public string BlobContainer { get; set; } = "productimages";
        public string QueueName { get; set; } = "orderqueue";
        public string FileShareName { get; set; } = "contracts";
    }
}
