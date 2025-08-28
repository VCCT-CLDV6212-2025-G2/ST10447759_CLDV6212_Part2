/*
 * FILE: QueueStorageService.cs
 * This service handles sending messages to Azure Queue Storage. It is used to
 * decouple processes, such as notifying a separate system that a new order has been created.
 * REFERENCE: Microsoft Docs, "Quickstart: Azure Queue Storage library - .NET"
 * https://learn.microsoft.com/en-us/azure/storage/queues/storage-queues-dotnet-get-started
 */
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using AzureRetailHub.Settings;
using System.Text;
using System.Text.Json;

namespace AzureRetailHub.Services
{
    public class QueueStorageService
    {
        private readonly QueueServiceClient _queueServiceClient;
        private readonly StorageOptions _opts;

        public QueueStorageService(IOptions<StorageOptions> opts)
        {
            _opts = opts.Value;
            _queueServiceClient = new QueueServiceClient(_opts.ConnectionString);
        }

        /// <summary>
        /// Sends a message to a specified Azure Storage Queue.
        /// </summary>
        /// <param name="queueName">The name of the queue.</param>
        /// <param name="message">The object to be sent as the message (will be serialized to JSON).</param>
        public async Task SendMessageAsync(string queueName, object message)
        {
            // NOTE: This method is used by the OrdersController. Explain the concept of a queue:
            // It's like a waiting line for messages. When an order is created, we put a message in the queue.
            // Another service (like an order processing system) could then pick up this message to handle fulfillment.
            // This meets the requirement of sending a queue message on order creation.
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            await queueClient.CreateIfNotExistsAsync();
            var json = JsonSerializer.Serialize(message);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await queueClient.SendMessageAsync(base64);
        }
    }
}
