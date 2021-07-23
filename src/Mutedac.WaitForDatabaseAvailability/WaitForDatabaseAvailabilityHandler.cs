using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.RDS;
using Amazon.RDS.Model;

using Lambdajection.Attributes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mutedac.WaitForDatabaseAvailability
{
    [Lambda(typeof(Startup))]
    public partial class WaitForDatabaseAvailabilityHandler
    {
        private readonly IAmazonRDS rdsClient;
        private readonly IAmazonLambda lambdaClient;
        private readonly IAmazonEventBridge eventsClient;
        private readonly ILogger<WaitForDatabaseAvailabilityHandler> logger;
        private readonly LambdaConfiguration configuration;

        public WaitForDatabaseAvailabilityHandler(
            IAmazonRDS rdsClient,
            IAmazonLambda lambdaClient,
            IAmazonEventBridge eventsClient,
            ILogger<WaitForDatabaseAvailabilityHandler> logger,
            IOptions<LambdaConfiguration> configuration
        )
        {
            this.rdsClient = rdsClient;
            this.lambdaClient = lambdaClient;
            this.eventsClient = eventsClient;
            this.logger = logger;
            this.configuration = configuration.Value;
        }

        public async Task<WaitForDatabaseAvailabilityResponse> Handle(WaitForDatabaseAvailabilityRequest request, CancellationToken cancellationToken = default)
        {
            var status = await GetDBClusterStatus(request.DatabaseName);

            if (status != "available")
            {
                return new WaitForDatabaseAvailabilityResponse { Message = "Database not available yet." };
            }

            _ = await eventsClient.DisableRuleAsync(new DisableRuleRequest
            {
                Name = configuration.WaitForDatabaseAvailabilityRuleName
            }, cancellationToken);

            _ = await lambdaClient.UpdateEventSourceMappingAsync(new UpdateEventSourceMappingRequest
            {
                UUID = configuration.DequeueEventSourceUUID,
                Enabled = true
            }, cancellationToken);

            return await Task.FromResult(new WaitForDatabaseAvailabilityResponse { });
        }

        private async Task<string> GetDBClusterStatus(string databaseName)
        {
            var response = await rdsClient.DescribeDBClustersAsync(new DescribeDBClustersRequest
            {
                DBClusterIdentifier = databaseName
            });

            var query = from cluster in response.DBClusters
                        where cluster.DBClusterIdentifier == databaseName
                        select cluster.Status;

            return query.FirstOrDefault() ?? "non-existent";
        }
    }
}
