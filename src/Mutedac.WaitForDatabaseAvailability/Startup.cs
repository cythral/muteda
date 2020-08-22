using Amazon.EventBridge;
using Amazon.Lambda;
using Amazon.RDS;

using Lambdajection.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mutedac.WaitForDatabaseAvailability
{
    public class Startup : ILambdaStartup
    {
        public IConfiguration Configuration { get; set; } = default!;

        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonRDS>();
            services.UseAwsService<IAmazonEventBridge>();
            services.UseAwsService<IAmazonLambda>();
        }
    }
}
