using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer4;
using IdentityServer4.EntityFramework.Interfaces;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NZFurs.Auth.Data
{
    public class SeedData
    {
        public static async Task EnsureSeedDataAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            using (var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = scope.ServiceProvider.GetService<IConfigurationDbContext>())
                {
                    var logger = scope.ServiceProvider.GetService<ILogger<SeedData>>();
                    await EnsureID4ConfigSeedDataAsync(context, logger, cancellationToken);
                }
            }
        }

        private static async Task EnsureID4ConfigSeedDataAsync(IConfigurationDbContext context, ILogger<SeedData> logger, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Seeding database [ID4 Configuration]...");

            if (!await context.Clients.AnyAsync(cancellationToken))
            {
                logger.LogInformation("Seeding Clients...");
                foreach (var client in Clients)
                {
                    await context.Clients.AddAsync(client.ToEntity(), cancellationToken);
                }
                await context.SaveChangesAsync();
            }

            //if (!await context.Clients.AnyAsync(cancellationToken))
            //{
            //    logger.LogInformation("Seeding Identity Resources...");
            //    foreach (var identityResources in IdentityResources)
            //    {
            //        await context.Clients.AddAsync(client.ToEntity(), cancellationToken);
            //    }
            //    await context.SaveChangesAsync();
            //}
        }

        private static readonly IEnumerable<Client> Clients = new List<Client>
        {
            new Client
            {
                ClientId = "",
                ClientName = "Insomnia Client (Development)",

                AllowAccessTokensViaBrowser = true,
                AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                },
                ClientSecrets =
                {
                    new Secret
                    {
                        Description = "Primary Secret",
                        Value = "",
                    },
                    new Secret
                    {
                        Description = "Secondary Secret",
                        Value = "",
                    }
                },
                Enabled = true,
                IncludeJwtId = true,
                RedirectUris =
                {
                    "http://fakeurl.infursec.furry.nz/callback",
                },
                RequireConsent = true,
            },
        };

        //private static readonly IEnumerable<IdentityResource> IdentityResources = new List<IdentityResource>
        //{

        //};
    }
}
