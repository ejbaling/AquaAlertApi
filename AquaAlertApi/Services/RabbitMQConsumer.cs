using AquaAlertApi.Contracts;
using AquaAlertApi.Data;
using MassTransit;
using LaunchDarkly.Sdk;
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

        public RabbitMQConsumer(ILogger<RabbitMQConsumer> logger, IConfiguration configuration, AppDbContext db)
        {
            _logger = logger;
            _configuration = configuration;
            LogEveryNMessages = configuration.GetValue<int>("RabbitMQ:LogEveryNMessages", 60);
            _db = db;

            // Initialize LaunchDarkly client
            var context = Context.New("context-key-123abc");
            var timeSpan = TimeSpan.FromSeconds(10);
            _ldClient = LdClient.Init(
                Configuration.Default("mob-010039af-0db7-4729-9e83-e02c0d53cf6d", LaunchDarkly.Sdk.Client.ConfigurationBuilder.AutoEnvAttributes.Enabled),
                context,
                timeSpan
            );

            if (_ldClient.Initialized)
                _logger.LogInformation("LaunchDarkly client successfully initialized!");
            else
                _logger.LogInformation("LaunchDarkly client failed to initialize");
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

                var flagValue = _ldClient.BoolVariation("save-to-database-feature", false);
                if (flagValue)
                    await _db.SaveChangesAsync();

                _logger.LogInformation("Water level log created with ID: {Id}", log.Id);

                Interlocked.Exchange(ref _messageCount, 0);
            }
        }
        
        public void Dispose()
        {
            _ldClient?.Dispose();
        }
    }    
}