using System;
using System.Collections.Generic;
using System.Security.Claims;
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

            if (!await context.IdentityResources.AnyAsync(cancellationToken))
            {
                logger.LogInformation("Seeding Identity Resources...");
                foreach (var identityResource in IdentityResources)
                {
                    await context.IdentityResources.AddAsync(identityResource.ToEntity(), cancellationToken);
                }
                await context.SaveChangesAsync();
            }
        }

        private static readonly IEnumerable<Client> Clients = new List<Client>
        {
            new Client
            {
                ClientId = "5B13A353-EAFC-4A42-933D-6A330A06AA8D",
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

        private static readonly IEnumerable<IdentityResource> IdentityResources = new List<IdentityResource>
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResource
            {
                Name = "Fursona",
                DisplayName = "Fursona",
                Description = "Allows this client access to your fursona's name and species (if available).",
                Enabled = true,
                Emphasize = false,
                Required = false,
                ShowInDiscoveryDocument = true,
                UserClaims = 
                {
                    "FursonaName",
                    "FursonaSpecies",
                }
            },
            new IdentityResource
            {
                Name = "RealName",
                DisplayName = "Real Name",
                Description = "Allows this client access to your full name and given name, which you use in general day-to-day life (if available). This may be (but doesn't have to be) your legal name.",
                Enabled = true,
                Emphasize = true,
                Required = false,
                ShowInDiscoveryDocument = true,
                UserClaims =
                {
                    ClaimTypes.Name,
                    ClaimTypes.GivenName,
                }
            },
            new IdentityResource
            {
                Name = "AgeCheck",
                DisplayName = "Age Check",
                Description = "Allows this client to check if you are over 18 (if provided). Your date of birth will not be provided.",
                Enabled = true,
                Emphasize = false,
                Required = false,
                ShowInDiscoveryDocument = true,
                UserClaims =
                {
                    "IsOver18",
                }
            },
            new IdentityResource
            {
                Name = "Birthday",
                DisplayName = "Birthday",
                Description = "Allows this client to know your date of birth (including the year) and infer your age.",
                Enabled = true,
                Emphasize = true,
                Required = false,
                ShowInDiscoveryDocument = true,
                UserClaims =
                {
                    "IsOver18",
                    ClaimTypes.DateOfBirth,
                }
            },
        };
    }
}
