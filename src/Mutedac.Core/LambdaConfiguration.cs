namespace Mutedac
{
    public class LambdaConfiguration
    {
        public string WaitForDatabaseAvailabilityRuleName { get; set; } = "";
        public string NotificationQueueUrl { get; set; } = "";
        public string DequeueEventSourceUUID { get; set; } = "";
    }
}