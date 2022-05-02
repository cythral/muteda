using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Mutedac.Cicd.DeployDriver;
using Mutedac.Cicd.Utils;

#pragma warning disable SA1516

await Microsoft.Extensions.Hosting.Host
.CreateDefaultBuilder()
.ConfigureAppConfiguration(configure =>
{
    configure.AddCommandLine(args, new Dictionary<string, string>
    {
        ["--environment"] = "CommandLineOptions:Environment",
        ["--artifacts-location"] = "CommandLineOptions:ArtifactsLocation",
    });
})
.ConfigureServices((context, services) =>
{
    services.Configure<CommandLineOptions>(context.Configuration.GetSection("CommandLineOptions"));
    services.AddSingleton<IHost, Mutedac.Cicd.DeployDriver.Host>();
    services.AddSingleton<StackDeployer>();
    services.AddSingleton<EcsDeployer>();
    services.AddSingleton<EcrUtils>();
})
.UseConsoleLifetime()
.Build()
.RunAsync();
