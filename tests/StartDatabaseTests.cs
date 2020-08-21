using System;
using System.Collections.Generic;
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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Mutedac.StartDatabase
{
    class StartDatabaseTests : TestSuite<StartDatabaseTests.Context>
    {
        private const string waitForDatabaseAvailabilityRuleName = "waitForDatabaseAvailabilityRuleName";
        private const string queueUrl = "queueUrl";
        private const string dequeueEventSourceUuid = "dequeueUuid";

        internal class Context : IContext
        {

#pragma warning disable CS8618, CS0649

            [Substitute] IAmazonRDS RdsClient;
            [Substitute] IAmazonSimpleNotificationService SnsClient;
            [Substitute] IAmazonEventBridge EventBridgeClient;
            [Substitute] IAmazonSQS SqsClient;
            [Substitute] IAmazonLambda LambdaClient;
            public StartDatabaseHandler StartDatabaseHandler;

#pragma warning restore CS8618, CS0649

            public Task Setup()
            {
                var logger = Substitute.For<ILogger<StartDatabaseHandler>>();
                var configuration = new OptionsWrapper<LambdaConfiguration>(new LambdaConfiguration
                {
                    WaitForDatabaseAvailabilityRuleName = waitForDatabaseAvailabilityRuleName,
                    NotificationQueueUrl = queueUrl,
                    DequeueEventSourceUUID = dequeueEventSourceUuid
                });

                LambdaClient.GetEventSourceMappingAsync(null).ReturnsForAnyArgs(new GetEventSourceMappingResponse
                {
                    State = "Disabled"
                });

                StartDatabaseHandler = new StartDatabaseHandler(RdsClient, SnsClient, EventBridgeClient, SqsClient, LambdaClient, logger, configuration);
                return Task.CompletedTask;
            }

            public void Deconstruct(
                out IAmazonRDS rdsClient,
                out IAmazonSimpleNotificationService snsClient,
                out IAmazonEventBridge eventsClient,
                out IAmazonSQS sqsClient,
                out IAmazonLambda lambdaClient,
                out StartDatabaseHandler handler
            )
            {
                rdsClient = RdsClient;
                snsClient = SnsClient;
                eventsClient = EventBridgeClient;
                sqsClient = SqsClient;
                lambdaClient = LambdaClient;
                handler = StartDatabaseHandler;
            }
        }

        [Test]
        public async Task StartDBCluster_ShouldBeCalled_IfStatusIsStopped()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "stopped"
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName
            });

            await rdsClient.Received().DescribeDBClustersAsync(
                Arg.Is<DescribeDBClustersRequest>(req => req.DBClusterIdentifier == databaseName)
            );

            await rdsClient.Received().StartDBClusterAsync(
                Arg.Is<StartDBClusterRequest>(req => req.DBClusterIdentifier == databaseName)
            );
        }

        [Test]
        public async Task EventSourceMapping_ShouldBeDisabled_IfStatusIsStopped_AndNotificationTopicProvided()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";
            var notificationTopic = "notificationTopic";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "stopped"
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = notificationTopic
            });

            await rdsClient.Received().DescribeDBClustersAsync(
                Arg.Is<DescribeDBClustersRequest>(req => req.DBClusterIdentifier == databaseName)
            );

            await lambdaClient.Received().UpdateEventSourceMappingAsync(
                Arg.Is<UpdateEventSourceMappingRequest>(req => req.UUID == dequeueEventSourceUuid && req.Enabled == false)
            );
        }

        [Test]
        public async Task EventSourceMapping_ShouldNotBeDisabled_IfStatusIsStopped_AndNotificationTopicNotProvided()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "stopped"
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
            });

            await rdsClient.Received().DescribeDBClustersAsync(
                Arg.Is<DescribeDBClustersRequest>(req => req.DBClusterIdentifier == databaseName)
            );

            await lambdaClient.DidNotReceive().UpdateEventSourceMappingAsync(
                Arg.Any<UpdateEventSourceMappingRequest>()
            );
        }

        [Test]
        public async Task StartDBCluster_IsNotCalled_IfStatusIsNotStopped()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "available"
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName
            });

            await rdsClient.Received().DescribeDBClustersAsync(
                Arg.Is<DescribeDBClustersRequest>(req => req.DBClusterIdentifier == databaseName)
            );

            await rdsClient.DidNotReceiveWithAnyArgs().StartDBClusterAsync(null);
        }

        [Test]
        public async Task EventSourceMapping_IsNotDisabled_IfStatusIsStartingOrAvailable([Values("starting", "available")] string status)
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = status
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName
            });

            await rdsClient.Received().DescribeDBClustersAsync(
                Arg.Is<DescribeDBClustersRequest>(req => req.DBClusterIdentifier == databaseName)
            );

            await lambdaClient.DidNotReceiveWithAnyArgs().UpdateEventSourceMappingAsync(null);
        }

        [Test]
        public async Task PublishTopic_IsCalledWithSuccess_IfDatabaseAlreadyStarted_AndTopicProvided()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";
            var topic = "topic";
            var token = "token";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "available"
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = topic,
                TaskToken = token
            });

            await snsClient.Received().PublishAsync(
                Arg.Is<PublishRequest>(req =>
                  req.TopicArn == topic &&
                  req.MessageAttributes["TaskToken"].StringValue == token &&
                  req.Message == "available"
                )
            );
        }

        [Test]
        public async Task PublishTopic_IsNotCalled_IfDatabaseAlreadyStarted_AndTopicNotProvided()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "available"
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = null,
            });

            await snsClient.DidNotReceiveWithAnyArgs().PublishAsync(null);
        }

        [Test]
        public async Task TokenAndTopic_AreQueued_IfDatabaseIsStoppedOrStarting_AndTopicProvided([Values("stopped", "starting")] string status)
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";
            var topic = "topic";
            var token = "token";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = status
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = topic,
                TaskToken = token,
            });

            var expectedMessage = Serialize(new
            {
                TaskToken = token,
                NotificationTopic = topic,
            });

            await sqsClient.Received().SendMessageAsync(Arg.Is<SendMessageRequest>(req =>
                req.MessageBody == expectedMessage &&
                req.QueueUrl == queueUrl
            ));
        }

        [Test]
        public async Task WaitForDatabaseAvailabilityRule_GetsEnabled_IfDatabaseIsStoppedOrStarting_AndTopicProvided([Values("stopped", "starting")] string status)
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";
            var topic = "topic";
            var token = "token";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = status
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = topic,
                TaskToken = token,
            });

            await eventsClient.Received().EnableRuleAsync(Arg.Is<EnableRuleRequest>(req => req.Name == waitForDatabaseAvailabilityRuleName));
        }

        [Test]
        public async Task WaitForDatabaseAvailabilityRule_DoesntGetEnabled_IfDatabaseIsStoppedOrStarting_AndTopicNotProvided([Values("stopped", "starting")] string status)
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = status
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = null,
            });

            await eventsClient.DidNotReceiveWithAnyArgs().EnableRuleAsync(null);
        }

        [Test]
        public async Task TokenAndTopic_AreNotQueued_IfDatabaseIsStoppedOrStarting_ButTopicNotProvided([Values("stopped", "starting")] string status)
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = status
                    }
                }
            });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
            });

            await sqsClient.DidNotReceiveWithAnyArgs().SendMessageAsync(null);
        }

        [Test]
        public async Task TokenAndTopic_AreNotQueued_IfStartingDatabaseFails()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";
            var topic = "topic";
            var token = "token";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "stopped"
                    }
                }
            });

            rdsClient.StartDBClusterAsync(Arg.Any<StartDBClusterRequest>()).Returns<StartDBClusterResponse>(x => { throw new Exception(); });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = topic,
                TaskToken = token,
            });

            await sqsClient.DidNotReceiveWithAnyArgs().SendMessageAsync(null);
        }

        [Test]
        public async Task PublishTopic_IsCalledWithFailure_IfStartingDatabaseFails_AndTopicProvided()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";
            var topic = "topic";
            var token = "token";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "stopped"
                    }
                }
            });

            rdsClient.StartDBClusterAsync(Arg.Any<StartDBClusterRequest>()).Returns<StartDBClusterResponse>(x => { throw new Exception(); });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = topic,
                TaskToken = token,
            });

            await snsClient.Received().PublishAsync(
                Arg.Is<PublishRequest>(req =>
                    req.Message == "failed" &&
                    req.TopicArn == topic &&
                    req.MessageAttributes["TaskToken"].StringValue == token
                )
            );
        }

        [Test]
        public async Task PublishTopic_IsNotCalled_IfStartingDatabaseFails_AndTopicNotProvided()
        {
            var (rdsClient, snsClient, eventsClient, sqsClient, lambdaClient, handler) = await GetContext();
            var databaseName = "databaseName";

            rdsClient.DescribeDBClustersAsync(Arg.Any<DescribeDBClustersRequest>()).Returns(new DescribeDBClustersResponse
            {
                DBClusters = new List<DBCluster>
                {
                    new DBCluster
                    {
                        DBClusterIdentifier = databaseName,
                        Status = "stopped"
                    }
                }
            });

            rdsClient.StartDBClusterAsync(Arg.Any<StartDBClusterRequest>()).Returns<StartDBClusterResponse>(x => { throw new Exception(); });

            await handler.Handle(new StartDatabaseRequest
            {
                DatabaseName = databaseName,
                NotificationTopic = null,
            });

            await snsClient.DidNotReceiveWithAnyArgs().PublishAsync(null);
        }
    }
}
