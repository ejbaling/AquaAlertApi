using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Client;

namespace AquaAlertApi.Services
{
    public interface ILaunchDarklyService
    {
        LdClient Client { get; }
    }

    public class LaunchDarklyService : ILaunchDarklyService, IDisposable
    {
        public LdClient Client { get; }
        private readonly ILogger<LaunchDarklyService> _logger;

        public LaunchDarklyService(IConfiguration configuration, ILogger<LaunchDarklyService> logger)
        {
            _logger = logger;
            var sdkKey = configuration.GetValue<string>("LaunchDarkly:SdkKey") 
                ?? throw new InvalidOperationException("LaunchDarkly SDK key not configured");
            
            var contextKey = configuration.GetValue<string>("LaunchDarkly:ContextKey") ?? "default-context";
            var timeout = configuration.GetValue<int>("LaunchDarkly:InitializationTimeout", 10);

            var context = Context.New(contextKey);
            var timeSpan = TimeSpan.FromSeconds(timeout);

            var config = Configuration.Default(
                sdkKey,
                LaunchDarkly.Sdk.Client.ConfigurationBuilder.AutoEnvAttributes.Enabled
            );

            Client = LdClient.Init(config, context, timeSpan);

            if (Client.Initialized)
                _logger.LogInformation("LaunchDarkly client successfully initialized!");
            else
                _logger.LogWarning("LaunchDarkly client failed to initialize within timeout period");
        }

        public void Dispose()
        {
            Client?.Dispose();
        }
    }
}