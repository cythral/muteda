using Amazon.StepFunctions;

using Lambdajection.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mutedac.StartDatabaseTaskCompleter
{
    public class Startup : ILambdaStartup
    {
        public IConfiguration Configuration { get; set; } = default!;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAmazonStepFunctions, AmazonStepFunctionsClient>();
            services.AddLogging(options => options.AddConsole());
        }
    }
}