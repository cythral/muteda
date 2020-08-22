using Amazon.EventBridge;
using Amazon.Lambda;
using Amazon.RDS;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

using Lambdajection.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mutedac.StartDatabase
{
    public class Startup : ILambdaStartup
    {
        public IConfiguration Configuration { get; set; } = default!;

        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonRDS>();
            services.UseAwsService<IAmazonSimpleNotificationService>();
            services.UseAwsService<IAmazonEventBridge>();
            services.UseAwsService<IAmazonSQS>();
            services.UseAwsService<IAmazonLambda>();
        }
    }
}
