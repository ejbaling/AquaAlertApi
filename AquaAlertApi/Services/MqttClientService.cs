
using MQTTnet;
using System.Text;

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
            var brokerIp = _configuration["MqttBroker:Ip"];
            var brokerPort = _configuration["MqttBroker:Port"];
            var username = _configuration["MqttBroker:Username"];
            var password = _configuration["MqttBroker:Password"];

            if (string.IsNullOrWhiteSpace(brokerIp) || string.IsNullOrWhiteSpace(brokerPort))
            {
                _logger.LogError("MQTT broker IP or port is not configured.");
                return;
            }

            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerIp, int.Parse(brokerPort))
                .WithClientId("AquaAlertApiClient")
                .WithCredentials(username, password)
                .Build();

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                _logger.LogInformation("Received mesage: {Parameter} cm", Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
                return Task.CompletedTask;
            };

            await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
            _logger.LogInformation("The MQTT client is connected.");

            var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter("/sh/water-distance").Build();
            await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            _logger.LogInformation("MQTT client subscribed to topic.");
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
            await mqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None);

            _logger.LogInformation("The MQTT client is disconnected.");
        }
    }
}
