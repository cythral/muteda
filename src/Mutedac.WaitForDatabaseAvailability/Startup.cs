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
            services.AddScoped<IAmazonRDS, AmazonRDSClient>();
            services.AddScoped<IAmazonEventBridge, AmazonEventBridgeClient>();
            services.AddScoped<IAmazonLambda, AmazonLambdaClient>();
            services.Configure<LambdaConfiguration>(Configuration.GetSection(LambdaConfiguration.SectionName));
        }
    }
}
