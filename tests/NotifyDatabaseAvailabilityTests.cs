using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Mutedac.NotifyDatabaseAvailability
{
    internal class NotifyDatabaseAvailabilityTests : TestSuite<NotifyDatabaseAvailabilityTests.Context>
    {
        internal class Context : IContext
        {

#pragma warning disable CS8618, CS0649, IDE0040, IDE0044

            [Substitute] IAmazonSimpleNotificationService SnsClient;
            public NotifyDatabaseAvailabilityHandler StartDatabaseHandler;

#pragma warning restore CS8618, CS0649, IDE0040, IDE0044

            public Task Setup()
            {
                StartDatabaseHandler = new NotifyDatabaseAvailabilityHandler(SnsClient);
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

            _ = await handler.Handle(new SQSEvent
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

            _ = await snsClient.Received().PublishAsync(
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
