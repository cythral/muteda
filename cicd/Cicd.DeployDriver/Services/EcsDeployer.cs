using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon.ECS;
using Amazon.ECS.Model;

using Task = System.Threading.Tasks.Task;

namespace Mutedac.Cicd.DeployDriver
{
    /// <summary>
    /// Service for deploying ECS services.
    /// </summary>
    public class EcsDeployer
    {
        private readonly IAmazonECS ecs = new AmazonECSClient();

        /// <summary>
        /// Deploys an ECS service so that at least one container is running.
        /// </summary>
        /// <param name="context">Details about the service to deploy.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The resulting task.</returns>
        public async Task Deploy(EcsDeployContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsRunning(context, cancellationToken))
            {
                Console.WriteLine($"The {context.ServiceName} service is already running.");
                return;
            }

            Console.WriteLine($"Deploying {context.ServiceName}...");
            await UpdateService(context, cancellationToken);
            await WaitForServiceToDeploy(context, cancellationToken);
            Console.WriteLine($"Successfully deployed {context.ServiceName}");
        }

        private async Task<Service> DescribeService(EcsDeployContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new DescribeServicesRequest
            {
                Cluster = context.ClusterName,
                Services = new List<string> { context.ServiceName },
            };

            var response = await ecs.DescribeServicesAsync(request, cancellationToken);
            var service = response.Services.First();

            return service;
        }

        private async Task<bool> IsRunning(EcsDeployContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var service = await DescribeService(context, cancellationToken);
            return service.RunningCount > 0;
        }

        private async Task UpdateService(EcsDeployContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new UpdateServiceRequest
            {
                Cluster = context.ClusterName,
                Service = context.ServiceName,
                DesiredCount = 1,
            };

            await ecs.UpdateServiceAsync(request, cancellationToken);
        }

        private async Task WaitForServiceToDeploy(EcsDeployContext context, CancellationToken cancellationToken)
        {
            int tries = 0;
            int maxTries = 60;

            while (!await IsRunning(context, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (tries++ > maxTries)
                {
                    throw new Exception("Deployment timed out.");
                }

                Console.WriteLine("Waiting for deployment to complete...");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }
}
