namespace Mutedac.StartDatabase
{
    public class StartDatabaseRequest
    {
        public string DatabaseName { get; set; } = "";
        public string? NotificationTopic { get; set; } = default;
        public string? TaskToken { get; set; } = default;
    }
}
