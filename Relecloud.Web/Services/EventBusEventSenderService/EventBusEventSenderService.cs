using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using Relecloud.Web.Models;
using System.Text;
using System.Threading.Tasks;

namespace Relecloud.Web.Services.EventBusEventSenderService
{
    public class EventBusEventSenderService : IEventSenderService
    {
        private readonly QueueClient queueClient;

        public EventBusEventSenderService(string connectionString, string queueName)
        {
            this.queueClient = new QueueClient(connectionString, queueName);
        }

        public void Initialize()
        {
        }

        public async Task SendEventAsync(Event eventMessage)
        {
            var body = JsonConvert.SerializeObject(eventMessage);
            var message = new Message(Encoding.UTF8.GetBytes(body));
            await this.queueClient.SendAsync(message);
        }
    }
}