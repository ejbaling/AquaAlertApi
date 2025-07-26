namespace AquaAlertApi.Contracts
{
    public class MqttMessage
    {
        public string? ClientId { get; set; }
        public decimal? Distance { get; set; }
        public string? Unit { get; set; }
    }
}