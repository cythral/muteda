using Amazon.EventBridge;
using Amazon.RDS;
using Amazon.SimpleNotificationService;

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
            services.AddLogging(options => options.AddConsole());
        }
    }
}