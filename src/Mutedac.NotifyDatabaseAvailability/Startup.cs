using Amazon.Lambda;
using Amazon.SimpleNotificationService;

using Lambdajection.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mutedac.NotifyDatabaseAvailability
{
    public class Startup : ILambdaStartup
    {
        public IConfiguration Configuration { get; set; } = default!;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceClient>();
            services.AddLogging(options => options.AddConsole());
            services.Configure<LambdaConfiguration>(Configuration.GetSection(LambdaConfiguration.SectionName));
        }
    }
}