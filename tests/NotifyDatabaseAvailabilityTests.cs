using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.SQSEvents;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Mutedac.NotifyDatabaseAvailability
{
    public class NotifyDatabaseAvailabilityTests : TestSuite<NotifyDatabaseAvailabilityTests.Context>
    {
        private const string queueUrl = "queueUrl";
        private const string dequeueEventSourceUuid = "dequeueUuid";

        new public class Context : TestSuite<Context>.Context
        {

#pragma warning disable CS8618, CS0649

            [Substitute] public IAmazonSimpleNotificationService SnsClient;
            [Substitute] public IAmazonEventBridge EventBridgeClient;
            [Substitute] public IConfiguration Configuration;
            public NotifyDatabaseAvailabilityHandler StartDatabaseHandler;

#pragma warning restore CS8618, CS0649

            public override Task Setup()
            {
                var logger = Substitute.For<ILogger<NotifyDatabaseAvailabilityHandler>>();
                var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Lambda:NotificationQueueUrl"] = queueUrl,
                    ["Lambda:DequeueEventSourceUUID"] = dequeueEventSourceUuid,
                }).Build();

                StartDatabaseHandler = new NotifyDatabaseAvailabilityHandler(SnsClient, logger, configuration);
                return Task.CompletedTask;
            }

            public void Deconstruct(
                out IAmazonSimpleNotificationService snsClient,
                out NotifyDatabaseAvailabilityHandler handler
            )
            {
                snsClient = SnsClient;
                handler = StartDatabaseHandler;
            }
        }

        [Test]
        public async Task PublishIsCalledForEachRecord()
        {
            var (snsClient, handler) = await GetContext();

            await handler.Handle(new SQSEvent
            {
                Records = new List<SQSEvent.SQSMessage>
                {
                    new SQSEvent.SQSMessage {
                        Body = Serialize(new
                        {
                            TaskToken = "token1",
                            NotificationTopic = "topic1"
                        })
                    }
                }
            });

            await snsClient.Received().PublishAsync(
                Arg.Is<PublishRequest>(req =>
                    req.Message == "available" &&
                    req.MessageAttributes["TaskToken"].StringValue == "token1" &&
                    req.TopicArn == "topic1"
                )
            );
        }
    }
}