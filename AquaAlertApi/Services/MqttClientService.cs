
using MQTTnet;
using System.Text;
using System.Text.Json;

namespace AquaAlertApi.Services.MqttClientService
{
    public class MqttClientService : IHostedService
    {
        private readonly ILogger<MqttClientService> _logger;
        private readonly IConfiguration _configuration;

        public MqttClientService(ILogger<MqttClientService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var mqttFactory = new MqttClientFactory();

            var mqttClient = mqttFactory.CreateMqttClient();
            var brokerIp = _configuration["Mqtt:Broker:Ip"];
            var brokerPort = _configuration["Mqtt:Broker:Port"];
            var username = _configuration["Mqtt:Broker:Username"];
            var password = _configuration["Mqtt:Broker:Password"];
            var clientId = _configuration["Mqtt:Client:Id"];

            if (string.IsNullOrWhiteSpace(brokerIp) || string.IsNullOrWhiteSpace(brokerPort))
            {
                _logger.LogError("MQTT broker IP or port is not configured.");
                return;
            }

            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerIp, int.Parse(brokerPort))
                .WithClientId(clientId)
                .WithCredentials(username, password)
                .Build();

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                var payloadString = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                try
                {
                    // Deserialize the JSON string to a dynamic object
                    var mqttMessage = JsonSerializer.Deserialize<MqttMessage>(payloadString);
                    _logger.LogInformation($"Received message: Client Id: {mqttMessage?.ClientId}, Distance: {mqttMessage?.Distance}, Unit: {mqttMessage?.Unit}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing the received message.");
                }

                return Task.CompletedTask;
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!await mqttClient.TryPingAsync(cancellationToken))
                    {
                        await mqttClient.ConnectAsync(mqttClientOptions, cancellationToken);
                        _logger.LogInformation("The MQTT client is connected.");

                        var subscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter("/sh/water-distance").Build();
                        await mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
                        _logger.LogInformation("MQTT client subscribed to topic.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "An error occurred while connecting to the MQTT broker.");
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested)
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            /*
            * This sample disconnects from the server with sending a DISCONNECT packet.
            * This way of disconnecting is treated as a clean disconnect which will not
            * trigger sending the last will etc.
            */

            var mqttFactory = new MqttClientFactory();
            var mqttClient = mqttFactory.CreateMqttClient();

            // Send a clean disconnect to the server by calling _DisconnectAsync_. Without this the TCP connection
            // gets dropped and the server will handle this as a non clean disconnect (see MQTT spec for details).
            var mqttClientDisconnectOptions = mqttFactory.CreateClientDisconnectOptionsBuilder().Build();
            await mqttClient.DisconnectAsync(mqttClientDisconnectOptions, cancellationToken);

            _logger.LogInformation("The MQTT client is disconnected.");
        }
    }

    public class MqttMessage
    {
        public string ClientId { get; set; }
        public decimal Distance { get; set; }
        public string Unit { get; set; }
    }
}
