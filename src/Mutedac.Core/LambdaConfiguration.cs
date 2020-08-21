namespace Mutedac
{
    public class LambdaConfiguration
    {
        public const string SectionName = "Lambda";

        public string WaitForDatabaseAvailabilityRuleName { get; set; } = "";
        public string NotificationQueueUrl { get; set; } = "";
        public string DequeueEventSourceUUID { get; set; } = "";
    }
}
