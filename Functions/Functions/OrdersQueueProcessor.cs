using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Extensions.Storage;
using Microsoft.Extensions.Logging;

namespace AzureRetailHub.Functions.Functions;

/// <summary>
/// Queue-triggered function that processes order messages and persists
/// them into Azure Table Storage (Orders table).
/// 
/// Config keys expected:
///   - StorageOptions:ConnectionString
///   - StorageOptions:OrdersTable
///   - StorageOptions:QueueName (in the QueueTrigger)
/// 
/// Behavior:
/// - Accepts Base64 or raw JSON queue messages.
/// - Supports "CreateOrUpdate" and "Delete" actions.
/// - Uses PartitionKey = "ORDER", RowKey = OrderId.
/// </summary>
public class OrdersQueueProcessor
{
    private readonly IConfiguration _config;
    public OrdersQueueProcessor(IConfiguration config) => _config = config;

    /// <summary>
    /// Contract for queue message payloads.
    /// Only the fields you need for persistence—kept minimal on purpose.
    /// </summary>
    public record OrderMessage(
        string Action,
        string OrderId,
        string CustomerId,
        string? Status,
        double? TotalAmount,
        DateTime? OrderDate,
        string? ItemsJson);

    /// <summary>
    /// Triggered automatically when a new message is available on the queue.
    /// </summary>
    [Function("OrdersQueueProcessor")]
    public async Task Run(
        [QueueTrigger("%StorageOptions:QueueName%", Connection = "AzureWebJobsStorage")] string message,
        FunctionContext context)
    {
        var log = context.GetLogger("OrdersQueueProcessor");

        try
        {
            // Accept both: try Base64 → fallback to raw JSON
            string json;
            try
            {
                json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(message));
            }
            catch (FormatException)
            {
                json = message; // Not Base64 — treat as plain JSON
            }

            // Deserialize loosely (case-insensitive for easier clients)
            var data = JsonSerializer.Deserialize<OrderMessage>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data is null)
            {
                log.LogWarning("Invalid message payload");
                return;
            }

            // Prepare Table client and ensure table exists
            var cs = _config["StorageOptions:ConnectionString"];
            var tableName = _config["StorageOptions:OrdersTable"];
            var table = new TableClient(cs, tableName);
            await table.CreateIfNotExistsAsync();

            // Route by action
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
                log.LogInformation("Upserted Order {orderId}", data.OrderId);
            }
            else if (string.Equals(data.Action, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                await table.DeleteEntityAsync("ORDER", data.OrderId);
                log.LogInformation("Deleted Order {orderId}", data.OrderId);
            }
            else
            {
                log.LogWarning("Unknown action: {action}", data.Action);
            }
        }
        catch (Exception ex)
        {
            // Bubble up for poison-queue handling while capturing context
            context.GetLogger("OrdersQueueProcessor").LogError(ex, "Failed processing order message");
            throw;
        }
    }
}
