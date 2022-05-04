using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Amazon.SecurityToken;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Mutedac.Cicd.Utils;

using YamlDotNet.Serialization;

namespace Mutedac.Cicd.BuildDriver
{
    /// <inheritdoc />
    public class Host : IHost
    {
        private static readonly string ConfigFile = ProjectRootDirectoryAttribute.ThisAssemblyProjectRootDirectory + "cicd/config.yml";
        private static readonly string IntermediateOutputDirectory = ProjectRootDirectoryAttribute.ThisAssemblyProjectRootDirectory + "obj/Cicd.Driver/";
        private static readonly string ToolkitStack = "cdk-toolkit";
        private static readonly string OutputsFile = IntermediateOutputDirectory + "cdk.outputs.json";
        private readonly EcrUtils ecrUtils;
        private readonly CommandLineOptions options;
        private readonly IHostApplicationLifetime lifetime;

        /// <summary>
        /// Initializes a new instance of the <see cref="Host" /> class.
        /// </summary>
        /// <param name="ecrUtils">Utilities for interacting with ECR.</param>
        /// <param name="options">Command line options.</param>
        /// <param name="lifetime">Service that controls the application lifetime.</param>
        /// <param name="serviceProvider">Object that provides access to the program's services.</param>
        public Host(
            EcrUtils ecrUtils,
            IOptions<CommandLineOptions> options,
            IHostApplicationLifetime lifetime,
            IServiceProvider serviceProvider
        )
        {
            this.ecrUtils = ecrUtils;
            this.options = options.Value;
            this.lifetime = lifetime;
            Services = serviceProvider;
        }

        /// <inheritdoc />
        public IServiceProvider Services { get; }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.SetCurrentDirectory(ProjectRootDirectoryAttribute.ThisAssemblyProjectRootDirectory + "cicd/Cicd.Artifacts");
            Directory.CreateDirectory(ProjectRootDirectoryAttribute.ThisAssemblyProjectRootDirectory + "bin/Cicd");
            var accountNumber = await GetCurrentAccountNumber(cancellationToken);

            await Step("Bootstrapping CDK", async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var command = new Command("cdk bootstrap", new Dictionary<string, object>
                {
                    ["--toolkit-stack-name"] = ToolkitStack,
                });

                await command.RunOrThrowError(
                    errorMessage: "Could not bootstrap CDK.",
                    cancellationToken: cancellationToken
                );
            });

            await Step("Deploying Artifacts Stack", async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var command = new Command("cdk deploy", new Dictionary<string, object>
                {
                    ["--toolkit-stack-name"] = ToolkitStack,
                    ["--require-approval"] = "never",
                    ["--outputs-file"] = OutputsFile,
                });

                await command.RunOrThrowError(
                    errorMessage: "Failed to deploy Artifacts Stack.",
                    cancellationToken: cancellationToken
                );
            });

            var outputs = await GetOutputs(cancellationToken);

            await Step("Create Environment Config Files", async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await CreateConfigFile("Development", cancellationToken);
                await CreateConfigFile("Production", cancellationToken);
            });

            await Step("Package Template", async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var command = new Command(
                    command: "aws cloudformation package",
                    options: new Dictionary<string, object>
                    {
                        ["--template-file"] = ProjectRootDirectoryAttribute.ThisAssemblyProjectRootDirectory + "cicd/template.yml",
                        ["--s3-bucket"] = outputs.BucketName,
                        ["--s3-prefix"] = options.Version,
                        ["--output-template-file"] = ProjectRootDirectoryAttribute.ThisAssemblyProjectRootDirectory + "bin/Cicd/template.yml",
                    }
                );

                await command.RunOrThrowError(
                    errorMessage: "Could not package CloudFormation template.",
                    cancellationToken: cancellationToken
                );
            });

            await Step("Upload Artifacts to S3", async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var command = new Command(
                    command: "aws s3 cp",
                    options: new Dictionary<string, object>
                    {
                        ["--recursive"] = true,
                    },
                    arguments: new[]
                    {
                        $"{ProjectRootDirectoryAttribute.ThisAssemblyProjectRootDirectory}bin/Cicd",
                        $"s3://{outputs.BucketName}/{options.Version}",
                    }
                );

                await command.RunOrThrowError(
                    errorMessage: "Could not upload artifacts to S3.",
                    cancellationToken: cancellationToken
                );
            });

            Console.WriteLine();
            Console.WriteLine($"::set-output name=artifacts-location::s3://{outputs.BucketName}/{options.Version}");

            lifetime.StopApplication();
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private static async Task<string> GetCurrentAccountNumber(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sts = new AmazonSecurityTokenServiceClient();
            var response = await sts.GetCallerIdentityAsync(new(), cancellationToken);
            return response.Account;
        }

        private static async Task<Outputs> GetOutputs(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var outputsFileStream = File.OpenRead(OutputsFile);
            var contents = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(outputsFileStream, cancellationToken: cancellationToken);
            var outputsText = contents!["mutedac-cicd"].GetRawText();

            return JsonSerializer.Deserialize<Outputs>(outputsText)!;
        }

        private static async Task CreateConfigFile(string environment, CancellationToken cancellationToken)
        {
            using var configFile = File.OpenRead(ConfigFile);
            using var configReader = new StreamReader(configFile);

            var deserializer = new DeserializerBuilder().Build();
            var config = deserializer.Deserialize<Config>(configReader);
            var parameters = new Dictionary<string, string>();

            foreach (var (parameterName, parameterDefinition) in config.Parameters)
            {
                var parameterValue = environment switch
                {
                    "Development" => parameterDefinition.Development,
                    "Production" => parameterDefinition.Production,
                    _ => throw new NotSupportedException(),
                };

                parameters.Add(parameterName, parameterValue);
            }

            var environmentConfig = new EnvironmentConfig
            {
                Tags = config.Tags,
                Parameters = parameters,
            };

            var destinationFilePath = $"{ProjectRootDirectoryAttribute.ThisAssemblyProjectRootDirectory}bin/Cicd/config.{environment}.json";
            using var destinationFile = File.OpenWrite(destinationFilePath);

            var options = new JsonSerializerOptions { WriteIndented = true };
            await JsonSerializer.SerializeAsync(destinationFile, environmentConfig, options, cancellationToken);
            Console.WriteLine($"Created config file for {environment} at {destinationFilePath}.");
        }

        private static async Task Step(string title, Func<Task> action)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n{title} ==========\n");
            Console.ResetColor();

            await action();
        }
    }
}
