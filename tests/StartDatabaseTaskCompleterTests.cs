using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Lambda.SNSEvents;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Mutedac.StartDatabaseTaskCompleter
{
    internal class StartDatabaseTaskCompleterTests : TestSuite<StartDatabaseTaskCompleterTests.Context>
    {
        internal class Context : IContext
        {

#pragma warning disable CS8618, CS0649, IDE0040, IDE0044

            [Substitute] IAmazonStepFunctions StepFunctionsClient;
            StartDatabaseTaskCompleterHandler StartDatabaseTaskCompleterHandler;

#pragma warning restore CS8618, CS0649, IDE0040, IDE0044

            public Task Setup()
            {
                StartDatabaseTaskCompleterHandler = new StartDatabaseTaskCompleterHandler(StepFunctionsClient);
                return Task.CompletedTask;
            }

            public void Deconstruct(
                out IAmazonStepFunctions stepFunctionsClient,
                out StartDatabaseTaskCompleterHandler handler
            )
            {
                stepFunctionsClient = StepFunctionsClient;
                handler = StartDatabaseTaskCompleterHandler;
            }
        }

        [Test]
        public async Task SendTaskSuccess_ShouldBeCalled_WithTheTaskToken_IfStatusIsAvailable()
        {
            var (stepFunctionsClient, handler) = await GetContext();
            var token = "token";

            _ = await handler.Handle(new SNSEvent
            {
                Records = new List<SNSEvent.SNSRecord>
                {
                    new SNSEvent.SNSRecord
                    {
                        Sns = new SNSEvent.SNSMessage
                        {
                            Message = "available",
                            MessageAttributes = new Dictionary<string, SNSEvent.MessageAttribute>
                            {
                                ["TaskToken"] = new SNSEvent.MessageAttribute { Value = token }
                            }
                        }
                    }
                }
            });

            var output = Serialize(new { Status = "available" });
            _ = await stepFunctionsClient.Received().SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req => req.Output == output && req.TaskToken == token));
        }

        [Test]
        public async Task SendTaskFailure_ShouldBeCalled_WithTheTaskToken_IfStatusIsNotAvailable()
        {
            var (stepFunctionsClient, handler) = await GetContext();
            var token = "token";

            _ = await handler.Handle(new SNSEvent
            {
                Records = new List<SNSEvent.SNSRecord>
                {
                    new SNSEvent.SNSRecord
                    {
                        Sns = new SNSEvent.SNSMessage
                        {
                            Message = "failed",
                            MessageAttributes = new Dictionary<string, SNSEvent.MessageAttribute>
                            {
                                ["TaskToken"] = new SNSEvent.MessageAttribute { Value = token }
                            }
                        }
                    }
                }
            });

            _ = await stepFunctionsClient.Received().SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.Cause == "failed" && req.TaskToken == token));
        }
    }
}
