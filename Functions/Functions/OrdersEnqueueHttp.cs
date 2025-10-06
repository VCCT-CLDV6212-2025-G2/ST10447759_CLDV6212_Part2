using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Queues;

namespace AzureRetailHub.Functions.Functions;

/// <summary>
/// HTTP endpoint to enqueue order messages to Azure Storage Queues.
/// 
/// Config keys expected:
///   - StorageOptions:ConnectionString
///   - StorageOptions:QueueName
/// 
/// Tip:
/// - We Base64 the message for safety (Queue service accepts both raw and Base64).
/// </summary>
public class OrdersEnqueueHttp
{
    private readonly IConfiguration _config;
    public OrdersEnqueueHttp(IConfiguration config) => _config = config;

    /// <summary>
    /// Route: POST /api/orders/enqueue
    /// Body: any JSON string that your queue processor understands.
    /// Example:
    /// {
    ///   "action":"CreateOrUpdate",
    ///   "orderId":"ORD-123",
    ///   "customerId":"CUST-001",
    ///   "status":"Pending",
    ///   "totalAmount": 1499.95
    /// }
    /// 
    /// Returns:
    ///   202 Accepted - "Order enqueued."
    ///   400 BadRequest - when body is empty
    /// </summary>
    [Function("OrdersEnqueue")]
    public async Task<HttpResponseData> EnqueueAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/enqueue")] HttpRequestData req)
    {
        // Read entire body as string; processor will validate shape later
        var payload = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(payload))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Empty body.");
            return bad;
        }

        // Connect to queue and ensure it exists
        var cs = _config["StorageOptions:ConnectionString"];
        var queueName = _config["StorageOptions:QueueName"];
        var queue = new QueueServiceClient(cs).GetQueueClient(queueName);
        await queue.CreateIfNotExistsAsync();

        // Base64-encode to avoid issues with special characters
        var msg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        await queue.SendMessageAsync(msg);

        var ok = req.CreateResponse(HttpStatusCode.Accepted);
        await ok.WriteStringAsync("Order enqueued.");
        return ok;
    }
}
