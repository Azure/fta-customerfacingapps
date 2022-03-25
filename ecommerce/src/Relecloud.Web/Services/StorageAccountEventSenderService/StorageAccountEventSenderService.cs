using Azure.Storage.Queues;
using Relecloud.Web.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relecloud.Web.Services.StorageAccountEventSenderService
{
    public class StorageAccountEventSenderService : IEventSenderService
    {
        private readonly QueueClient queue;

        public StorageAccountEventSenderService(string connectionString, string queueName)
        {
            this.queue = new QueueClient(connectionString, queueName, new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            });
        }

        public void Initialize()
        {
            this.queue.CreateIfNotExistsAsync().Wait();
        }

        public async Task SendEventAsync(Event eventMessage)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            };
            var body = JsonSerializer.Serialize(eventMessage, options);
            await this.queue.SendMessageAsync(body);
        }
    }
}