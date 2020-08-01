using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading.Tasks;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda.Core;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using NSubstitute;

using NUnit.Framework;

namespace Mutedac.StartDatabase
{
    public class StartDatabaseTests : TestSuite<StartDatabaseTests.Context>
    {
        private const string waitForDatabaseAvailabilityRuleName = "waitForDatabaseAvailabilityRuleName";

        new public class Context : TestSuite<Context>.Context
        {

#pragma warning disable CS8618, CS0649

            [Substitute] public IAmazonRDS RdsClient;
            [Substitute] public IAmazonSimpleNotificationService SnsClient;
            [Substitute] public IAmazonEventBridge EventBridgeClient;
            [Substitute] public IFileSystem FileSystem;
            [Substitute] public IConfiguration Configuration;
            public StartDatabaseHandler StartDatabaseHandler;

#pragma warning restore CS8618, CS0649

            public override Task Setup()
            {
                var logger = Substitute.For<ILogger<StartDatabaseHandler>>();
                var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Lambda:WaitForDatabaseAvailabilityRuleName"] = waitForDatabaseAvailabilityRuleName
                }).Build();

                StartDatabaseHandler = new StartDatabaseHandler(RdsClient, SnsClient, EventBridgeClient, FileSystem, logger, configuration);
                return Task.CompletedTask;
            }

            public void Deconstruct(
                out IAmazonRDS rdsClient,
                out IAmazonSimpleNotificationService snsClient,
                out IAmazonEventBridge eventsClient,
                out IFileSystem fileSystem,
                out StartDatabaseHandler handler
            )
            {
                rdsClient = RdsClient;
                snsClient = SnsClient;
                eventsClient = EventBridgeClient;
                fileSystem = FileSystem;
                handler = StartDatabaseHandler;
            }
        }

        [Test]
        public async Task StartDBCluster_ShouldBeCalled_IfStatusIsStopped()
        {
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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
        public async Task StartDBCluster_IsNotCalled_IfStatusIsNotStopped()
        {
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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
        public async Task PublishTopic_IsCalledWithSuccess_IfDatabaseAlreadyStarted_AndTopicProvided()
        {
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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
        public async Task TokenAndTopic_AreWrittenToDisk_IfDatabaseIsStoppedOrStarting_AndTopicProvided([Values("stopped", "starting")] string status)
        {
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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

            await fileSystem.File.Received().AppendAllTextAsync(Arg.Is("/waitlist.txt"), Arg.Is($"{topic} {token}\n"));
        }

        [Test]
        public async Task WaitForDatabaseAvailabilityRule_GetsEnabled_IfDatabaseIsStoppedOrStarting_AndTopicProvided([Values("stopped", "starting")] string status)
        {
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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
        public async Task TokenAndTopic_AreNotWrittenToDisk_IfDatabaseIsStoppedOrStarting_ButTopicNotProvided([Values("stopped", "starting")] string status)
        {
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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

            await fileSystem.File.DidNotReceiveWithAnyArgs().AppendAllTextAsync(null, null);
        }

        [Test]
        public async Task TokenAndTopic_AreNotWrittenToDisk_IfStartingDatabaseFails()
        {
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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

            await fileSystem.File.DidNotReceiveWithAnyArgs().AppendAllTextAsync(null, null);
        }

        [Test]
        public async Task PublishTopic_IsCalledWithFailure_IfStartingDatabaseFails_AndTopicProvided()
        {
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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
            var (rdsClient, snsClient, eventsClient, fileSystem, handler) = await GetContext();
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