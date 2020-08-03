using System;
using System.Linq;
using System.Threading.Tasks;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.RDS;
using Amazon.RDS.Model;

using Lambdajection.Attributes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Mutedac.WaitForDatabaseAvailability
{
    [Lambda(Startup = typeof(Startup))]
    public partial class WaitForDatabaseAvailabilityHandler
    {
        private IAmazonRDS rdsClient;
        private IAmazonLambda lambdaClient;
        private IAmazonEventBridge eventsClient;
        private ILogger<WaitForDatabaseAvailabilityHandler> logger;
        private LambdaConfiguration configuration;

        public WaitForDatabaseAvailabilityHandler(
            IAmazonRDS rdsClient,
            IAmazonLambda lambdaClient,
            IAmazonEventBridge eventsClient,
            ILogger<WaitForDatabaseAvailabilityHandler> logger,
            IConfiguration configuration
        )
        {
            this.rdsClient = rdsClient;
            this.lambdaClient = lambdaClient;
            this.eventsClient = eventsClient;
            this.logger = logger;
            this.configuration = configuration.GetSection("Lambda").Get<LambdaConfiguration>();
        }

        public async Task<WaitForDatabaseAvailabilityResponse> Handle(WaitForDatabaseAvailabilityRequest request, ILambdaContext context = default!)
        {
            var status = await GetDBClusterStatus(request.DatabaseName);

            if (status != "available")
            {
                return new WaitForDatabaseAvailabilityResponse { Message = "Database not available yet." };
            }

            await eventsClient.DisableRuleAsync(new DisableRuleRequest
            {
                Name = configuration.WaitForDatabaseAvailabilityRuleName
            });

            await lambdaClient.UpdateEventSourceMappingAsync(new UpdateEventSourceMappingRequest
            {
                UUID = configuration.DequeueEventSourceUUID,
                Enabled = true
            });

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
