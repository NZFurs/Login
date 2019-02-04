using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NZFurs.Auth.Data;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NZFurs.Auth
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                Log.Information("Building web host");
                var host = CreateWebHostBuilder(args).Build();

                Log.Information("Checking for seed data");
                await SeedData.EnsureSeedDataAsync(host.Services);

                Log.Information("Running web host");
                host.Run();

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true);
                    config.AddUserSecrets<Startup>();
                    config.AddEnvironmentVariables(prefix: "NZFURS:AUTH:");
                    config.AddCommandLine(args);

                    if (!hostingContext.HostingEnvironment.IsDevelopment())
                    {
                        var preVaultConfig = config.Build();
                        config.AddAzureKeyVault(
                            $"https://{preVaultConfig["Azure:KeyVault:KeyVault"]}.vault.azure.net/",
                            preVaultConfig["Azure:ActiveDirectory:ClientId"],
                            preVaultConfig["Azure:ActiveDirectory:ClientSecrets:0"] // TODO: How do we fall back to secondary secrets?
                        );
                    }

                    config.Validate(File.ReadAllText("configurationschema.json"), throwOnError: true);
                })
                .UseStartup<Startup>()
                .UseKestrel(c => c.AddServerHeader = false)
                .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                );
    }
}
