using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Lambdajection.Attributes;

using static System.Text.Json.JsonSerializer;

namespace Mutedac.NotifyDatabaseAvailability
{
    [Lambda(typeof(Startup))]
    public partial class NotifyDatabaseAvailabilityHandler
    {
        private readonly IAmazonSimpleNotificationService snsClient;

        public NotifyDatabaseAvailabilityHandler(
            IAmazonSimpleNotificationService snsClient
        )
        {
            this.snsClient = snsClient;
        }

        public async Task<NotifyDatabaseAvailabilityResponse> Handle(SQSEvent request, CancellationToken cancellationToken = default)
        {
            var tasks = request.Records.Select(record => PublishForRecord(record, cancellationToken));
            await Task.WhenAll(tasks);
            return new NotifyDatabaseAvailabilityResponse { Message = "Published all messages" };
        }

        private async Task PublishForRecord(SQSEvent.SQSMessage record, CancellationToken cancellationToken)
        {
            var message = Deserialize<QueueMessage>(record.Body);
            _ = await snsClient.PublishAsync(new PublishRequest
            {
                TopicArn = message?.NotificationTopic,
                Message = "available",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["TaskToken"] = new MessageAttributeValue
                    {
                        StringValue = message?.TaskToken,
                        DataType = "String"
                    }
                }
            }, cancellationToken);
        }
    }
}
