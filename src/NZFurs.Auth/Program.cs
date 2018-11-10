using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NZFurs.Auth.Data;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NZFurs.Auth
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.Title = "NZFurs OpenID Connect Provider";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.File(@"Data/Log/log.txt")
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}", theme: AnsiConsoleTheme.Literate)
                .CreateLogger();

            var host = CreateWebHostBuilder(args).Build();

            await SeedData.EnsureSeedDataAsync(host.Services);

            host.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("Data/Config/appsettings.json", optional: false, reloadOnChange: false);
                    config.AddJsonFile($"Data/Config/appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json");
                    config.AddUserSecrets<Startup>();
                    config.AddEnvironmentVariables(prefix: "NZFURS__AUTH__");
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
                })
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog();
                })
                .UseStartup<Startup>();
    }
}
