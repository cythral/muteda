using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.SNSEvents;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Lambdajection.Attributes;

using static System.Text.Json.JsonSerializer;

namespace Mutedac.StartDatabaseTaskCompleter
{
    [Lambda(typeof(Startup))]
    public partial class StartDatabaseTaskCompleterHandler
    {
        private readonly IAmazonStepFunctions stepFunctionsClient;

        public StartDatabaseTaskCompleterHandler(
            IAmazonStepFunctions stepFunctionsClient
        )
        {
            this.stepFunctionsClient = stepFunctionsClient;
        }

        public async Task<StartDatabaseTaskCompleterResponse> Handle(SNSEvent request, CancellationToken cancellationToken = default)
        {
            var tasks = request.Records.Select(record => CompleteTask(record.Sns, cancellationToken));
            await Task.WhenAll(tasks);

            return new StartDatabaseTaskCompleterResponse
            {
                Message = "Notified all pending tasks"
            };
        }

        public async Task CompleteTask(SNSEvent.SNSMessage message, CancellationToken cancellationToken)
        {
            if (message.Message == "available")
            {
                _ = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
                {
                    Output = Serialize(new { Status = message.Message }),
                    TaskToken = message.MessageAttributes["TaskToken"].Value
                }, cancellationToken);

                return;
            }

            _ = await stepFunctionsClient.SendTaskFailureAsync(new SendTaskFailureRequest
            {
                Cause = message.Message,
                TaskToken = message.MessageAttributes["TaskToken"].Value
            }, cancellationToken);
        }
    }
}
