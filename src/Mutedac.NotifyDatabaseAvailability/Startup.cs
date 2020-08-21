using Amazon.SimpleNotificationService;

using Lambdajection.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mutedac.NotifyDatabaseAvailability
{
    public class Startup : ILambdaStartup
    {
        public IConfiguration Configuration { get; set; } = default!;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceClient>();
            services.Configure<LambdaConfiguration>(Configuration.GetSection(LambdaConfiguration.SectionName));
        }
    }
}
