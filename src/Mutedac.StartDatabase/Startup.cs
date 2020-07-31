using Amazon.RDS;

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
            services.AddScoped<IAmazonRDS, AmazonRDSClient>();
        }
    }
}