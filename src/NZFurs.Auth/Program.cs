using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NZFurs.Auth
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
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
                .UseStartup<Startup>();
    }
}
