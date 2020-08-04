using Amazon.EventBridge;
using Amazon.Lambda;
using Amazon.RDS;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

using Lambdajection.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mutedac.StartDatabase
{
    public class Startup : ILambdaStartup
    {
        public IConfiguration Configuration { get; set; } = default!;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAmazonRDS, AmazonRDSClient>();
            services.AddScoped<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceClient>();
            services.AddScoped<IAmazonEventBridge, AmazonEventBridgeClient>();
            services.AddScoped<IAmazonSQS, AmazonSQSClient>();
            services.AddScoped<IAmazonLambda, AmazonLambdaClient>();
            services.AddLogging(options => options.AddConsole());
        }
    }
}