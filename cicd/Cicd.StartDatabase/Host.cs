using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

namespace Mutedac.Cicd.StartDatabase
{
    /// <inheritdoc />
    public class Host : IHost
    {
        private readonly IHostApplicationLifetime lifetime;
        private readonly DatabaseService database;

        /// <summary>
        /// Initializes a new instance of the <see cref="Host" /> class.
        /// </summary>
        /// <param name="database">Service for interacing with the database.</param>
        /// <param name="lifetime">Service that controls the application lifetime.</param>
        /// <param name="serviceProvider">Object that provides access to the program's services.</param>
        public Host(
            DatabaseService database,
            IHostApplicationLifetime lifetime,
            IServiceProvider serviceProvider
        )
        {
            this.database = database;
            this.lifetime = lifetime;
            Services = serviceProvider;
        }

        /// <inheritdoc />
        public IServiceProvider Services { get; }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Step("Starting Database", async () =>
            {
                var name = await database.GetClusterName(cancellationToken);
                var isRunning = await database.IsDatabaseRunning(name, cancellationToken);

                if (isRunning)
                {
                    return;
                }

                await database.StartDatabase(name, cancellationToken);
                for (int tries = 0; !await database.IsDatabaseRunning(name, cancellationToken) && tries < 120; tries++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Console.WriteLine("Waiting for database to start...");
                    await Task.Delay(5000, cancellationToken);
                }
            });

            Console.WriteLine("Database started successfully!");
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

        private static async Task Step(string title, Func<Task> action)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n{title} ==========\n");
            Console.ResetColor();

            await action();
        }
    }
}
