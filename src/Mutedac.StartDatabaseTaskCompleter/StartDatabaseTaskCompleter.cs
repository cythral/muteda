using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;

using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SNSEvents;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Lambdajection.Attributes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using static System.Text.Json.JsonSerializer;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Mutedac.StartDatabaseTaskCompleter
{
    [Lambda(Startup = typeof(Startup))]
    public partial class StartDatabaseTaskCompleterHandler
    {
        private IAmazonStepFunctions stepFunctionsClient;
        private ILogger<StartDatabaseTaskCompleterHandler> logger;

        public StartDatabaseTaskCompleterHandler(
            IAmazonStepFunctions stepFunctionsClient,
            ILogger<StartDatabaseTaskCompleterHandler> logger
        )
        {
            this.stepFunctionsClient = stepFunctionsClient;
            this.logger = logger;
        }

        public async Task<StartDatabaseTaskCompleterResponse> Handle(SNSEvent request, ILambdaContext context = default!)
        {
            var tasks = request.Records.Select(record => CompleteTask(record.Sns));
            await Task.WhenAll(tasks);
            return await Task.FromResult(new StartDatabaseTaskCompleterResponse { });
        }

        public async Task CompleteTask(SNSEvent.SNSMessage message)
        {
            if (message.Message == "available")
            {
                await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
                {
                    Output = Serialize(new { Status = message.Message }),
                    TaskToken = message.MessageAttributes["TaskToken"].Value
                });

                return;
            }

            await stepFunctionsClient.SendTaskFailureAsync(new SendTaskFailureRequest
            {
                Cause = message.Message,
                TaskToken = message.MessageAttributes["TaskToken"].Value
            });
        }
    }
}
