using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

using Lambdajection.Attributes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static System.Text.Json.JsonSerializer;

using SNSMessageAttributeValue = Amazon.SimpleNotificationService.Model.MessageAttributeValue;

namespace Mutedac.StartDatabase
{
    [Lambda(typeof(Startup))]
    public partial class StartDatabaseHandler
    {
        private const string StartedStatus = "available";
        private const string StoppedStatus = "stopped";

        private readonly IAmazonRDS rdsClient;
        private readonly IAmazonSimpleNotificationService snsClient;
        private readonly IAmazonEventBridge eventsClient;
        private readonly IAmazonSQS sqsClient;
        private readonly IAmazonLambda lambdaClient;
        private readonly ILogger<StartDatabaseHandler> logger;
        private readonly LambdaConfiguration configuration;

        public StartDatabaseHandler(
            IAmazonRDS rdsClient,
            IAmazonSimpleNotificationService snsClient,
            IAmazonEventBridge eventsClient,
            IAmazonSQS sqsClient,
            IAmazonLambda lambdaClient,
            ILogger<StartDatabaseHandler> logger,
            IOptions<LambdaConfiguration> configuration
        )
        {
            this.rdsClient = rdsClient;
            this.snsClient = snsClient;
            this.eventsClient = eventsClient;
            this.sqsClient = sqsClient;
            this.lambdaClient = lambdaClient;
            this.logger = logger;
            this.configuration = configuration.Value;
        }

        public async Task<StartDatabaseResponse> Handle(StartDatabaseRequest request, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Recieved request: ", request);
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
                    var startDbClusterRequest = new StartDBClusterRequest
                    {
                        DBClusterIdentifier = request.DatabaseName
                    };

                    _ = await rdsClient.StartDBClusterAsync(startDbClusterRequest, cancellationToken);
                }

                if (request.NotificationTopic != null)
                {
                    await DisableNotifyDatabaseAvailabilityEventSourceMapping();

                    var sendMessageRequest = new SendMessageRequest
                    {
                        QueueUrl = configuration.NotificationQueueUrl,
                        MessageBody = Serialize(new QueueMessage { NotificationTopic = request.NotificationTopic, TaskToken = request.TaskToken! })
                    };

                    var enableRuleRequest = new EnableRuleRequest
                    {
                        Name = configuration.WaitForDatabaseAvailabilityRuleName
                    };

                    _ = await sqsClient.SendMessageAsync(sendMessageRequest, cancellationToken);
                    _ = await eventsClient.EnableRuleAsync(enableRuleRequest, cancellationToken);
                }

                return new StartDatabaseResponse { Message = $"Starting database cluster {request.DatabaseName}" };
            }
            catch (Exception e)
            {
                logger.LogDebug($"An error occurred while attempting to start the database: {e.Message} {e.StackTrace}");

                await NotifyIfNotificationTopicProvided("failed", request.NotificationTopic, request.TaskToken);

                return new StartDatabaseResponse { Message = "An error occurred while attempting to start the database." };
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
                _ = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = notificationTopic,
                    Message = status,
                    MessageAttributes = new Dictionary<string, SNSMessageAttributeValue>
                    {
                        ["TaskToken"] = new SNSMessageAttributeValue
                        {
                            StringValue = taskToken,
                            DataType = "String"
                        }
                    }
                });
            }
        }

        private async Task DisableNotifyDatabaseAvailabilityEventSourceMapping()
        {
            _ = await lambdaClient.UpdateEventSourceMappingAsync(new UpdateEventSourceMappingRequest
            {
                UUID = configuration.DequeueEventSourceUUID,
                Enabled = false,
            });


            var disabled = false;
            while (!disabled)
            {
                var getEventSourceRequest = new GetEventSourceMappingRequest { UUID = configuration.DequeueEventSourceUUID };
                var response = await lambdaClient.GetEventSourceMappingAsync(getEventSourceRequest);

                disabled = response.State.ToLower() == "disabled";
                await Task.Delay(500);
            }
        }
    }
}
