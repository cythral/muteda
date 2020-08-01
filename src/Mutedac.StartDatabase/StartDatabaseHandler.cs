using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Lambdajection.Attributes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Mutedac.StartDatabase
{
    [Lambda(Startup = typeof(Startup))]
    public partial class StartDatabaseHandler
    {
        private const string StartedStatus = "available";
        private const string StoppedStatus = "stopped";

        private IAmazonRDS rdsClient;
        private IAmazonSimpleNotificationService snsClient;
        private IAmazonEventBridge eventsClient;
        private IFileSystem fileSystem;
        private ILogger<StartDatabaseHandler> logger;
        private LambdaConfiguration configuration;

        public StartDatabaseHandler(
            IAmazonRDS rdsClient,
            IAmazonSimpleNotificationService snsClient,
            IAmazonEventBridge eventsClient,
            IFileSystem fileSystem,
            ILogger<StartDatabaseHandler> logger,
            IConfiguration configuration
        )
        {
            this.rdsClient = rdsClient;
            this.snsClient = snsClient;
            this.eventsClient = eventsClient;
            this.fileSystem = fileSystem;
            this.logger = logger;
            this.configuration = configuration.GetSection("Lambda").Get<LambdaConfiguration>();
        }

        public async Task<StartDatabaseResponse> Handle(StartDatabaseRequest request, ILambdaContext context = default!)
        {
            var status = await GetDBClusterStatus(request.DatabaseName);

            if (status == StartedStatus)
            {
                await NotifyIfNotificationTopicProvided(status, request.NotificationTopic, request.TaskToken);

                return new StartDatabaseResponse { Message = $"Database cluster {request.DatabaseName} started." };
            }

            try
            {
                if (status == StoppedStatus)
                {
                    await rdsClient.StartDBClusterAsync(new StartDBClusterRequest
                    {
                        DBClusterIdentifier = request.DatabaseName
                    });
                }

                if (request.NotificationTopic != null)
                {
                    await fileSystem.File.AppendAllTextAsync(configuration.WaitlistFilePath, $"{request.NotificationTopic} {request.TaskToken}\n");
                    await eventsClient.EnableRuleAsync(new EnableRuleRequest { Name = configuration.WaitForDatabaseAvailabilityRuleName });
                }

                return new StartDatabaseResponse { Message = $"Starting database cluster {request.DatabaseName}" };
            }
            catch (Exception e)
            {
                logger.LogDebug($"An error occurred while attempting to start the database: {e.Message} {e.StackTrace}");

                await NotifyIfNotificationTopicProvided("failed", request.NotificationTopic, request.TaskToken);

                return new StartDatabaseResponse { Message = $"An error occurred while attempting to start the database." };
            }
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

        private async Task NotifyIfNotificationTopicProvided(string status, string? notificationTopic, string? taskToken)
        {
            if (notificationTopic != null)
            {
                await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = notificationTopic,
                    Message = status,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        ["TaskToken"] = new MessageAttributeValue { StringValue = taskToken }
                    }
                });
            }
        }

        public static async Task Main(string[] args)
        {
            await StartDatabaseHandler.Run(new StartDatabaseRequest
            {
                DatabaseName = "test"
            });
        }
    }
}
