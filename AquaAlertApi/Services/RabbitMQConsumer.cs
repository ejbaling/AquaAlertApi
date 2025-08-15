using AquaAlertApi.Contracts;
using AquaAlertApi.Data;
using MassTransit;
using LaunchDarkly.Sdk.Client;

namespace AquaAlertApi.Services
{
    public class RabbitMQConsumer : IConsumer<MqttMessage>
    {
        private readonly ILogger<RabbitMQConsumer> _logger;
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;
        private static int _messageCount = 0;
        private readonly int LogEveryNMessages = 60; // every 5 minutes if messages are sent every 5 seconds. This can be adjusted via configuration.
        private readonly LdClient _ldClient;

        public RabbitMQConsumer(ILogger<RabbitMQConsumer> logger,
        IConfiguration configuration,
        AppDbContext db,
        ILaunchDarklyService launchDarklyService)
        {
            _logger = logger;
            _configuration = configuration;
            LogEveryNMessages = configuration.GetValue<int>("RabbitMQ:LogEveryNMessages", 60);
            _db = db;
            _ldClient = launchDarklyService.Client;
        }

        public async Task Consume(ConsumeContext<MqttMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation($"ClientId: {message.ClientId}, WaterLevel: {message.Distance}, Unit: {message.Unit}");


            if ((_messageCount % LogEveryNMessages) == 0)
            {
                var log = new WaterLevelLog
                {
                    WaterLevelCm = message.Distance ?? 0,
                    TankId = 3,
                    ClientId = message.ClientId
                };

                await _db.WaterLevelLogs.AddAsync(log);

                var flagValue = _ldClient.BoolVariation("save-to-database-feature", false);
                if (flagValue)
                    await _db.SaveChangesAsync();

                _logger.LogInformation("Water level log created with ID: {Id}", log.Id);

                Interlocked.Exchange(ref _messageCount, 0);
            }

            Interlocked.Increment(ref _messageCount);
        }
        
        public void Dispose()
        {
            _ldClient?.Dispose();
        }
    }    
}