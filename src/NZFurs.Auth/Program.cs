using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NZFurs.Auth
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                Log.Information("Starting web host");
                CreateWebHostBuilder(args).Build().Run();
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
                .UseStartup<Startup>()
                .UseKestrel(c => c.AddServerHeader = false)
                .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                );
    }
}
