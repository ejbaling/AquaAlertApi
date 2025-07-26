using AquaAlertApi.Contracts;
using AquaAlertApi.Data;
using MassTransit;

namespace AquaAlertApi.Services
{
    public class RabbitMQConsumer : IConsumer<MqttMessage>
    {
        private readonly ILogger<RabbitMQConsumer> _logger;
        private readonly AppDbContext _db;
        private static int _messageCount = 0;
        private const int LogEveryNMessages = 60; // every 5 minutes if messages are sent every 5 seconds

        public RabbitMQConsumer(ILogger<RabbitMQConsumer> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task Consume(ConsumeContext<MqttMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation($"ClientId: {message.ClientId}, WaterLevel: {message.Distance}, Unit: {message.Unit}");

            Interlocked.Increment(ref _messageCount);

            if ((_messageCount % LogEveryNMessages) == 0)
            {
                var log = new WaterLevelLog
                {
                    WaterLevelCm = message.Distance ?? 0,
                    TankId = 3
                };

                await _db.WaterLevelLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Water level log created with ID: {Id}", log.Id);

                 Interlocked.Exchange(ref _messageCount, 0);
            }
        }
    }    
}