using System;
using System.Reflection.Metadata;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.RDS;
using Amazon.RDS.Model;

using Lambdajection.Attributes;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Mutedac.StartDatabase
{
    [Lambda(Startup = typeof(Startup))]
    public partial class StartDatabaseHandler
    {
        private IAmazonRDS rdsClient;

        public StartDatabaseHandler(IAmazonRDS rdsClient)
        {
            this.rdsClient = rdsClient;
        }

        public async Task<StartDatabaseResponse> Handle(StartDatabaseRequest request, ILambdaContext context = default!)
        {
            var startResponse = await rdsClient.StartDBClusterAsync(new StartDBClusterRequest
            {
                DBClusterIdentifier = request.DatabaseName
            });

            return new StartDatabaseResponse
            {
                Message = $"Starting database cluster {request.DatabaseName}",
            };
        }
    }
}
