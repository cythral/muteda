namespace Mutedac.StartDatabase
{
    public class LambdaConfiguration
    {
        public string WaitForDatabaseAvailabilityRuleName { get; set; } = "";
        public string WaitlistFilePath { get; set; } = "/waitlist.txt";
    }
}