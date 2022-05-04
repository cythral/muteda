using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.RDS;
using Amazon.RDS.Model;

namespace Mutedac.Cicd.StartDatabase
{
    public class DatabaseService
    {
        private readonly IAmazonCloudFormation cloudformation;
        private readonly IAmazonRDS rds;

        public DatabaseService(
            IAmazonCloudFormation cloudformation,
            IAmazonRDS rds
        )
        {
            this.cloudformation = cloudformation;
            this.rds = rds;
        }


        public async Task<string> GetClusterName(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exports = await cloudformation.ListExportsAsync(new(), cancellationToken);
            var query = from export in exports.Exports where export.Name == "mutedac:ClusterName" select export.Value;
            return query.First();
        }

        public async Task<bool> IsDatabaseRunning(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new DescribeDBClustersRequest { DBClusterIdentifier = name };
            var response = await rds.DescribeDBClustersAsync(request, cancellationToken);
            var query = from database in response.DBClusters where database.Status.ToLower() == "available" select 1;
            return query.Any();
        }

        public async Task StartDatabase(string name, CancellationToken cancellationToken)
        {
            var request = new StartDBClusterRequest { DBClusterIdentifier = name };
            await rds.StartDBClusterAsync(request, cancellationToken);
        }
    }
}
