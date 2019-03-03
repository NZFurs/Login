using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.Interfaces;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using NZFurs.Auth.Data;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NZFurs.Auth
{
    public class Program
    {
        private static Dictionary<string, X509Certificate2> _certificates = new Dictionary<string, X509Certificate2>();
        private static KeyVaultClient _keyVaultClient;

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

                Log.Information("Ensure database migrations are applied");
                using (var scope = host.Services.CreateScope())
                {
                    using (var context = scope.ServiceProvider.GetService<IConfigurationDbContext>() as DbContext)
                        await context.Database.MigrateAsync();
                    using (var context = scope.ServiceProvider.GetService<IPersistedGrantDbContext>() as DbContext)
                        await context.Database.MigrateAsync();
                    using(var context = scope.ServiceProvider.GetService<ApplicationDbContext>())
                        await context.Database.MigrateAsync();
                }

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
                            GetKeyVaultClient(preVaultConfig.GetConnectionString("AzureServiceTokenProvider")),
                            new DefaultKeyVaultSecretManager()
                        );
                    }

                    config.Validate(File.ReadAllText("configurationschema.json"), throwOnError: true);
                })
                .UseStartup<Startup>()
                .UseKestrel((hostContext, options) =>
                {
                    options.AddServerHeader = false;
                    options.ConfigureHttpsDefaults(h =>
                    {
                        h.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                        h.CheckCertificateRevocation = true;
                        h.ServerCertificateSelector = (features, name) =>
                        {
                            var hostname = name ?? hostContext.Configuration["DefaultHostname"];
                            if (!_certificates.ContainsKey(hostname))
                            {
                                var keyVaultClient = GetKeyVaultClient(hostContext.Configuration.GetConnectionString("AzureServiceTokenProvider"));
                                var pfxBase64String = _keyVaultClient.GetSecretAsync($"https://{hostContext.Configuration["Azure:KeyVault:KeyVault"]}.vault.azure.net/", hostname).GetAwaiter().GetResult().Value;
                                _certificates[hostname] = new X509Certificate2(Convert.FromBase64String(pfxBase64String));
                            }

                            return _certificates[hostname];
                        };
                    });
                })
                .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                    .ReadFrom.Configuration(hostingContext.Configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                );

        private static KeyVaultClient GetKeyVaultClient(string connectionString)
        {
            if (_keyVaultClient == null)
            {
                var azureServiceTokenProvider = new AzureServiceTokenProvider(connectionString);
                _keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback)
                );
            }
            return _keyVaultClient;
        }
    }
}
