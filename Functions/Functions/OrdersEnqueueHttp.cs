using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Queues;

namespace AzureRetailHub.Functions.Functions;

public class OrdersEnqueueHttp
{
    private readonly IConfiguration _config;
    public OrdersEnqueueHttp(IConfiguration config) => _config = config;

    [Function("OrdersEnqueue")]
    public async Task<HttpResponseData> EnqueueAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/enqueue")] HttpRequestData req)
    {
        var payload = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(payload))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Empty body.");
            return bad;
        }

        var cs = _config["StorageOptions:ConnectionString"];
        var queueName = _config["StorageOptions:QueueName"];
        var queue = new QueueServiceClient(cs).GetQueueClient(queueName);
        await queue.CreateIfNotExistsAsync();

        // Either send raw or Base64; both work. Keep your Base64 if you prefer.
        var msg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
        await queue.SendMessageAsync(msg);

        var ok = req.CreateResponse(HttpStatusCode.Accepted);
        await ok.WriteStringAsync("Order enqueued.");
        return ok;
    }
}
