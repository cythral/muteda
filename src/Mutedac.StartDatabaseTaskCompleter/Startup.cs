using Amazon.StepFunctions;

using Lambdajection.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mutedac.StartDatabaseTaskCompleter
{
    public class Startup : ILambdaStartup
    {
        public IConfiguration Configuration { get; set; } = default!;

        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonStepFunctions>();
        }
    }
}
