namespace Mutedac.NotifyDatabaseAvailability
{
    public class QueueMessage
    {
        public string NotificationTopic { get; set; } = "";
        public string TaskToken { get; set; } = "";
    }
}
