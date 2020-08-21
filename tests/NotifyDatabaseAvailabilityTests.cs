using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.EventBridge;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Mutedac.NotifyDatabaseAvailability
{
    class NotifyDatabaseAvailabilityTests : TestSuite<NotifyDatabaseAvailabilityTests.Context>
    {
        private const string queueUrl = "queueUrl";
        private const string dequeueEventSourceUuid = "dequeueUuid";

        internal class Context : IContext
        {

#pragma warning disable CS8618, CS0649

            [Substitute] IAmazonSimpleNotificationService SnsClient;
            public NotifyDatabaseAvailabilityHandler StartDatabaseHandler;

#pragma warning restore CS8618, CS0649

            public Task Setup()
            {
                var logger = Substitute.For<ILogger<NotifyDatabaseAvailabilityHandler>>();
                var configuration = new OptionsWrapper<LambdaConfiguration>(new LambdaConfiguration
                {
                    NotificationQueueUrl = queueUrl,
                    DequeueEventSourceUUID = dequeueEventSourceUuid
                });

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
                    req.MessageAttributes["TaskToken"].DataType == "String" &&
                    req.TopicArn == "topic1"
                )
            );
        }
    }
}
