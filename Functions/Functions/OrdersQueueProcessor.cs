using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Extensions.Storage;
using Microsoft.Extensions.Logging;

namespace AzureRetailHub.Functions.Functions;

public class OrdersQueueProcessor
{
    private readonly IConfiguration _config;
    public OrdersQueueProcessor(IConfiguration config) => _config = config;

    public record OrderMessage(string Action, string OrderId, string CustomerId, string? Status, double? TotalAmount,
                               DateTime? OrderDate, string? ItemsJson);

    [Function("OrdersQueueProcessor")]
    public async Task Run(
    [QueueTrigger("%StorageOptions:QueueName%", Connection = "AzureWebJobsStorage")] string message,
    FunctionContext context)
    {
        var log = context.GetLogger("OrdersQueueProcessor");
        try
        {
            // 👇 Accept both: try Base64 → fallback to raw JSON
            string json;
            try
            {
                json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(message));
            }
            catch (FormatException)
            {
                json = message; // not Base64 — treat as plain JSON
            }

            var data = System.Text.Json.JsonSerializer.Deserialize<OrderMessage>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data is null) { log.LogWarning("Invalid message payload"); return; }

            var cs = _config["StorageOptions:ConnectionString"];
            var tableName = _config["StorageOptions:OrdersTable"];
            var table = new TableClient(cs, tableName);
            await table.CreateIfNotExistsAsync();

            if (string.Equals(data.Action, "CreateOrUpdate", StringComparison.OrdinalIgnoreCase))
            {
                var entity = new TableEntity("ORDER", data.OrderId)
                {
                    ["CustomerId"] = data.CustomerId ?? "",
                    ["Status"] = data.Status ?? "Pending",
                    ["TotalAmount"] = data.TotalAmount ?? 0.0,
                    ["OrderDate"] = data.OrderDate ?? DateTime.UtcNow,
                    ["ItemsJson"] = data.ItemsJson ?? "[]"
                };
                await table.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            }
            else if (string.Equals(data.Action, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                await table.DeleteEntityAsync("ORDER", data.OrderId);
            }
            else
            {
                log.LogWarning("Unknown action: {action}", data.Action);
            }
        }
        catch (Exception ex)
        {
            context.GetLogger("OrdersQueueProcessor").LogError(ex, "Failed processing order message");
            throw;
        }
    }

}
