using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.SNSEvents;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using NSubstitute;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Mutedac.StartDatabaseTaskCompleter
{
    public class StartDatabaseTaskCompleterTests : TestSuite<StartDatabaseTaskCompleterTests.Context>
    {
        private const string queueUrl = "queueUrl";
        private const string dequeueEventSourceUuid = "dequeueUuid";

        new public class Context : TestSuite<Context>.Context
        {

#pragma warning disable CS8618, CS0649

            [Substitute] public IAmazonStepFunctions StepFunctionsClient;
            [Substitute] public IAmazonEventBridge EventBridgeClient;
            [Substitute] public IConfiguration Configuration;
            public StartDatabaseTaskCompleter StartDatabaseTaskCompleterHandler;

#pragma warning restore CS8618, CS0649

            public override Task Setup()
            {
                var logger = Substitute.For<ILogger<StartDatabaseTaskCompleter>>();

                StartDatabaseTaskCompleterHandler = new StartDatabaseTaskCompleter(StepFunctionsClient, logger);
                return Task.CompletedTask;
            }

            public void Deconstruct(
                out IAmazonStepFunctions stepFunctionsClient,
                out StartDatabaseTaskCompleter handler
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

            await handler.Handle(new SNSEvent
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
            await stepFunctionsClient.Received().SendTaskSuccessAsync(Arg.Is<SendTaskSuccessRequest>(req => req.Output == output && req.TaskToken == token));
        }

        [Test]
        public async Task SendTaskFailure_ShouldBeCalled_WithTheTaskToken_IfStatusIsNotAvailable()
        {
            var (stepFunctionsClient, handler) = await GetContext();
            var token = "token";

            await handler.Handle(new SNSEvent
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

            await stepFunctionsClient.Received().SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req => req.Cause == "failed" && req.TaskToken == token));
        }
    }
}