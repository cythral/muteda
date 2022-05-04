using Amazon.CloudFormation;
using Amazon.RDS;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Mutedac.Cicd.StartDatabase;

#pragma warning disable SA1516

await Microsoft.Extensions.Hosting.Host
.CreateDefaultBuilder()
.ConfigureServices((context, services) =>
{
    services.AddSingleton<IHost, Mutedac.Cicd.StartDatabase.Host>();
    services.AddSingleton<IAmazonCloudFormation, AmazonCloudFormationClient>();
    services.AddSingleton<IAmazonRDS, AmazonRDSClient>();
    services.AddSingleton<DatabaseService>();
})
.UseConsoleLifetime()
.Build()
.RunAsync();
