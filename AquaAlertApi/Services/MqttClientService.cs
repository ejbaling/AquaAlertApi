
using MQTTnet;
using System.Text;

public class MqttClientService : IHostedService
{
    private readonly ILogger<MqttClientService> _logger;

    public MqttClientService(ILogger<MqttClientService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        /*
         * This sample creates a simple MQTT client and connects to a public broker.
         *
         * Always dispose the client when it is no longer used.
         * The default version of MQTT is 3.1.1.
         */

        var mqttFactory = new MqttClientFactory();

        var mqttClient = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer("10.0.0.120")
            .WithCredentials("mqtt-user", "Ng7tov!KhVv3")
            .Build();

        // Setup message handling before connecting so that queued messages
        // are also handled properly. When there is no event handler attached all
        // received messages get lost.
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            _logger.LogInformation("Received mesage: {0}", Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
            return Task.CompletedTask;
        };

        // This will throw an exception if the server is not available.
        // The result from this message returns additional data which was sent
        // from the server. Please refer to the MQTT protocol specification for details.
        var response = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
        _logger.LogInformation(response.ResultCode.ToString());
        _logger.LogInformation("The MQTT client is connected.");

        // Subscribe to a topic
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

        using var mqttClient = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("homeassistant-1.tailc2bda.ts.net").Build();

        // Calling _DisconnectAsync_ will send a DISCONNECT packet before closing the connection.
        // Using a reason code requires MQTT version 5.0.0!
        await mqttClient.DisconnectAsync(MqttClientDisconnectOptionsReason.ImplementationSpecificError);

        _logger.LogInformation("The MQTT client is disconnected.");
    }
}
