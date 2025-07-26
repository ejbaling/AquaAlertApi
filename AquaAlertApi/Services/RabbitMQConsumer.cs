using AquaAlertApi.Contracts;
using MassTransit;

namespace AquaAlertApi.Services
{
    public class RabbitMQConsumer : IConsumer<MqttMessage>
    {
        private readonly ILogger<RabbitMQConsumer> _logger;

        public RabbitMQConsumer(ILogger<RabbitMQConsumer> logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<MqttMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Received message: ClientId={ClientId}, Distance={Distance}, Unit={Unit}",
                message.ClientId, message.Distance, message.Unit);

            // Process the message as needed
            // For example, you could save it to a database or trigger some action

            return Task.CompletedTask;
        }
    }    
}