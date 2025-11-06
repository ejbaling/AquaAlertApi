using AquaAlertApi.Contracts;
using AquaAlertApi.Data;
using MassTransit;
using LaunchDarkly.Sdk.Client;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

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
        private readonly HttpClient _httpClient;
        private readonly string? _telegramBotToken;
        private readonly string? _telegramChatId;
        // Alerting configuration
        private readonly decimal _alertDistanceThreshold;
        private readonly TimeSpan _alertCooldown;
        private readonly bool _onlyOnCrossing;
    private readonly TimeSpan _stateRetention;
    private readonly int _cleanupEveryNMessages;

        // Per-client state to support debounce/deduplication and crossing detection.
        // Static so state is shared across consumer instances in the process.
        private static readonly ConcurrentDictionary<string, ClientAlertState> _clientStates = new();
    private static int _cleanupCounter = 0;

        private sealed class ClientAlertState
        {
            public DateTime LastSentUtc;
            public bool LastAbove;
            public DateTime LastSeenUtc;
        }

        public RabbitMQConsumer(ILogger<RabbitMQConsumer> logger,
        IConfiguration configuration,
        AppDbContext db,
        ILaunchDarklyService launchDarklyService,
        IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            LogEveryNMessages = configuration.GetValue<int>("RabbitMQ:LogEveryNMessages", 60);
            _db = db;
            _ldClient = launchDarklyService.Client;
            _httpClient = httpClientFactory.CreateClient();

            // Read Telegram configuration (bot token and chat id)
            _telegramBotToken = configuration.GetValue<string>("Telegram:BotToken");
            _telegramChatId = configuration.GetValue<string>("Telegram:ChatId");

            // Read alerting configuration
            _alertDistanceThreshold = configuration.GetValue<decimal>("Alerts:DistanceThreshold", 100m);
            var cooldownMinutes = configuration.GetValue<int>("Alerts:CooldownMinutes", 10);
            _alertCooldown = TimeSpan.FromMinutes(cooldownMinutes);
            _onlyOnCrossing = configuration.GetValue<bool>("Alerts:OnlyOnCrossing", true);

            // State retention / cleanup config
            var retentionHours = configuration.GetValue<int>("Alerts:StateRetentionHours", 24);
            _stateRetention = TimeSpan.FromHours(retentionHours);
            _cleanupEveryNMessages = configuration.GetValue<int>("Alerts:CleanupEveryNMessages", 1000);
        }

        public async Task Consume(ConsumeContext<MqttMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation($"ClientId: {message.ClientId}, WaterLevel: {message.Distance}, Unit: {message.Unit}");


            if ((_messageCount % LogEveryNMessages) == 0)
            {
                var utcNow = DateTimeOffset.UtcNow;

                var phZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var manilaNow = TimeZoneInfo.ConvertTime(utcNow, phZone); // DateTimeOffset +08:00

                var log = new WaterLevelLog
                {
                    WaterLevelCm = message.Distance ?? 0,
                    TankId = 3,
                    ClientId = message.ClientId,
                    LoggedAt = utcNow, // timestamptz UTC
                    // store local as DateTime without offset
                    LoggedAtLocal = DateTime.SpecifyKind(manilaNow.DateTime, DateTimeKind.Unspecified),
                };
                await _db.WaterLevelLogs.AddAsync(log);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Water level log created with ID: {Id}", log.Id);

                Interlocked.Exchange(ref _messageCount, 0);
            }

            // Evaluate alert conditions with debounce/deduplication and optional crossing-only behavior
            try
            {
                var clientId = string.IsNullOrWhiteSpace(message.ClientId) ? "<unknown>" : message.ClientId!;
                var currentAbove = message.Distance.HasValue && message.Distance.Value > _alertDistanceThreshold;

                var nowUtc = DateTime.UtcNow;

                // Get or create per-client state
                var state = _clientStates.GetOrAdd(clientId, _ => new ClientAlertState { LastSentUtc = DateTime.MinValue, LastAbove = false, LastSeenUtc = nowUtc });

                // Determine whether we should send:
                // - If OnlyOnCrossing: send only when currentAbove == true and previous LastAbove == false
                // - If not OnlyOnCrossing: send whenever currentAbove == true
                var crossingCondition = !_onlyOnCrossing || (currentAbove && !state.LastAbove);

                var cooldownPassed = (nowUtc - state.LastSentUtc) >= _alertCooldown;

                var shouldSend = currentAbove && crossingCondition && cooldownPassed;

                if (shouldSend)
                {
                    if (string.IsNullOrWhiteSpace(_telegramBotToken) || string.IsNullOrWhiteSpace(_telegramChatId))
                    {
                        _logger.LogWarning("Telegram bot token or chat id is not configured. Skipping Telegram notification.");
                    }
                    else
                    {
                        var text = $"⚠️ Water level alert for client {clientId}: distance = {message.Distance} {message.Unit ?? ""} (>{_alertDistanceThreshold})";
                        var payload = new { chat_id = _telegramChatId, text };
                        var json = JsonSerializer.Serialize(payload);
                        using var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var url = $"https://api.telegram.org/bot{_telegramBotToken}/sendMessage";
                        var resp = await _httpClient.PostAsync(url, content);
                        if (!resp.IsSuccessStatusCode)
                        {
                            var body = await resp.Content.ReadAsStringAsync();
                            _logger.LogError("Failed to send Telegram message. Status: {Status}, Body: {Body}", resp.StatusCode, body);
                        }
                        else
                        {
                            _logger.LogInformation("Telegram alert sent for client {ClientId} (distance {Distance})", clientId, message.Distance);
                            // Update last sent time
                            state.LastSentUtc = nowUtc;
                        }
                    }
                }

                // Always update LastAbove and LastSeenUtc so crossing detection and retention work next time
                state.LastAbove = currentAbove;
                state.LastSeenUtc = nowUtc;

                // Periodic cleanup to avoid unbounded growth of the dictionary. Run once every _cleanupEveryNMessages processed.
                try
                {
                    var counter = Interlocked.Increment(ref _cleanupCounter);
                    if (_cleanupEveryNMessages > 0 && (counter % _cleanupEveryNMessages) == 0)
                    {
                        var removed = 0;
                        foreach (var kvp in _clientStates)
                        {
                            var age = nowUtc - kvp.Value.LastSeenUtc;
                            if (age > _stateRetention)
                            {
                                if (_clientStates.TryRemove(kvp.Key, out _))
                                    removed++;
                            }
                        }
                        if (removed > 0)
                            _logger.LogInformation("Cleaned up {Removed} stale clientStates entries older than {Retention}", removed, _stateRetention);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error during clientStates cleanup (non-fatal)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while sending Telegram notification");
            }

            Interlocked.Increment(ref _messageCount);
        }
        
        public void Dispose()
        {
            _ldClient?.Dispose();
        }
    }    
}