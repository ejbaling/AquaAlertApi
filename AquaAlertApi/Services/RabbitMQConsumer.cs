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
        private readonly decimal _alertLevelThreshold;
        private readonly decimal _overflowThreshold;
        private readonly decimal _refillThreshold;
        private readonly TimeSpan _refillAlertCooldown;
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
            public bool LastBelow;
            public bool LastOverflow;
            public bool LastRefilled;
            public DateTime LastRefillSentUtc;
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
            _alertLevelThreshold = configuration.GetValue<decimal>("Alerts:LevelThreshold", 150m);
            _overflowThreshold = configuration.GetValue<decimal>("Alerts:OverflowThreshold", 160m);
            _refillThreshold = configuration.GetValue<decimal>("Alerts:RefillThreshold", 155m);
            var refillCooldownHours = configuration.GetValue<int>("Alerts:RefillCooldownHours", 12);
            _refillAlertCooldown = TimeSpan.FromHours(refillCooldownHours);
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
            var sensorHeight = 200m;
            // An overflow condition is when the measured water level exceeds the configured overflow threshold.
            // This also handles sensors that might be submerged and report very low distances.
            var sensorGap = 0m; // set to 0 temporarily, assuming the sensor is the full level.
            var distance = message.Distance ?? 0;
            distance = distance > sensorGap ? distance - sensorGap  : 0;
            var waterLevel =  sensorHeight - distance;
            _logger.LogInformation("ClientId: {ClientId}, WaterLevel: {WaterLevel:F2}, Unit: {Unit}",
    message.ClientId ?? "<null>", waterLevel, message.Unit ?? "<null>");


            if ((_messageCount % LogEveryNMessages) == 0)
            {
                var utcNow = DateTimeOffset.UtcNow;

                var phZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var manilaNow = TimeZoneInfo.ConvertTime(utcNow, phZone); // DateTimeOffset +08:00

                var log = new WaterLevelLog
                {
                    WaterLevelCm = waterLevel,
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
                var currentBelow = waterLevel < _alertLevelThreshold;
                var currentOverflow = waterLevel > _overflowThreshold;
                var currentRefilled = waterLevel >= _refillThreshold;

                var nowUtc = DateTime.UtcNow;

                // Get or create per-client state
                var state = _clientStates.GetOrAdd(clientId, _ => new ClientAlertState { LastSentUtc = DateTime.MinValue, LastBelow = false, LastOverflow = false, LastRefilled = false, LastRefillSentUtc = DateTime.MinValue, LastSeenUtc = nowUtc });

                // Determine whether we should send low-level or overflow alerts.
                var crossingConditionLow = !_onlyOnCrossing || (currentBelow && !state.LastBelow);
                var crossingConditionOverflow = !_onlyOnCrossing || (currentOverflow && !state.LastOverflow);
                var crossingConditionRefill = !_onlyOnCrossing || (currentRefilled && !state.LastRefilled);

                var cooldownPassed = (nowUtc - state.LastSentUtc) >= _alertCooldown;

                // Only allow a refill notification if the last recorded "refill sent" time was at least the configured window ago.
                var refillCooldownPassed = (nowUtc - state.LastRefillSentUtc) >= _refillAlertCooldown;

                var shouldSendLow = currentBelow && crossingConditionLow && cooldownPassed;
                var shouldSendOverflow = currentOverflow && crossingConditionOverflow && cooldownPassed;
                var shouldSendRefill = currentRefilled && crossingConditionRefill && cooldownPassed && refillCooldownPassed;

                if (shouldSendLow || shouldSendOverflow || shouldSendRefill)
                {
                    if (string.IsNullOrWhiteSpace(_telegramBotToken) || string.IsNullOrWhiteSpace(_telegramChatId))
                    {
                        _logger.LogWarning("Telegram bot token or chat id is not configured. Skipping Telegram notification.");
                    }
                    else
                    {
                        string text;
                        if (shouldSendLow)
                        {
                            text = $"⚠️ Low water alert for client {clientId}: Water Level = {waterLevel} {message.Unit ?? ""} ( < {_alertLevelThreshold})";
                        }
                        else if (shouldSendOverflow)
                        {
                            text = $"🚨 Overflow alert for client {clientId}: Water Level = {waterLevel} {message.Unit ?? ""} ( > {_overflowThreshold})";
                        }
                        else
                        {
                            text = $"✅ Tank refilled for client {clientId}: Water Level = {waterLevel} {message.Unit ?? ""} (≥ {_refillThreshold})";
                        }
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
                            _logger.LogInformation("Telegram alert sent for client {ClientId} (Water Level {WaterLevel})", clientId, waterLevel);
                            // Update last sent time
                            state.LastSentUtc = nowUtc;
                            if (shouldSendRefill)
                                state.LastRefillSentUtc = nowUtc;
                        }
                    }
                }

                // Always update LastBelow/LastOverflow/LastRefilled and LastSeenUtc so crossing detection and retention work next time
                state.LastBelow = currentBelow;
                state.LastOverflow = currentOverflow;
                state.LastRefilled = currentRefilled;
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