using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Lambdajection.Attributes;

using static System.Text.Json.JsonSerializer;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Mutedac.NotifyDatabaseAvailability
{
    [Lambda(Startup = typeof(Startup))]
    public partial class NotifyDatabaseAvailabilityHandler
    {
        private readonly IAmazonSimpleNotificationService snsClient;

        public NotifyDatabaseAvailabilityHandler(
            IAmazonSimpleNotificationService snsClient
        )
        {
            this.snsClient = snsClient;
        }

        public async Task<NotifyDatabaseAvailabilityResponse> Handle(SQSEvent request, ILambdaContext context = default!)
        {
            var tasks = request.Records.Select(PublishForRecord);
            await Task.WhenAll(tasks);
            return new NotifyDatabaseAvailabilityResponse { Message = "Published all messages" };
        }

        private async Task PublishForRecord(SQSEvent.SQSMessage record)
        {
            var message = Deserialize<QueueMessage>(record.Body);
            await snsClient.PublishAsync(new PublishRequest
            {
                TopicArn = message.NotificationTopic,
                Message = "available",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["TaskToken"] = new MessageAttributeValue
                    {
                        StringValue = message.TaskToken,
                        DataType = "String"
                    }
                }
            });
        }
    }
}
