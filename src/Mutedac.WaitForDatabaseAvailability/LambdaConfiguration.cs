using Lambdajection.Attributes;

namespace Mutedac.WaitForDatabaseAvailability
{
    [LambdaOptions(typeof(WaitForDatabaseAvailabilityHandler), "Lambda")]
    public class LambdaConfiguration
    {
        public string WaitForDatabaseAvailabilityRuleName { get; set; } = "";
        public string DequeueEventSourceUUID { get; set; } = "";
    }
}
