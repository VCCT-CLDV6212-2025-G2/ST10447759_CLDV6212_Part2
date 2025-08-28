using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureRetailHub.Models
{
    public class OrderDetailViewModel
    {
        public OrderDto Order { get; set; }
        public CustomerDto Customer { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();

        // Helper to calculate the total price of the order
        public decimal TotalPrice => Items.Sum(item => item.TotalPrice);

        public void ParseItems()
        {
            if (!string.IsNullOrEmpty(Order.ItemsJson))
            {
                try
                {
                    var parsedItems = JsonSerializer.Deserialize<List<OrderItem>>(Order.ItemsJson);
                    if (parsedItems != null)
                    {
                        Items = parsedItems;
                    }
                }
                catch (JsonException)
                {
                    // Handle cases where JSON is invalid
                    Items = new List<OrderItem>();
                }
            }
        }
    }

    // Represents a single item within an order
    public class OrderItem
    {
        // This property will be in the JSON
        [JsonPropertyName("productId")]
        public string ProductId { get; set; }

        // This property will be populated by the controller after fetching the product
        public string ProductName { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        public decimal TotalPrice => Quantity * Price;
    }
}