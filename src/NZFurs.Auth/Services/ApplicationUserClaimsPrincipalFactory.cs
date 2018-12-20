using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NZFurs.Auth.Models;

namespace NZFurs.Auth.Services
{
    public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
    {
        public ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, optionsAccessor)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            identity.AddClaimIfNotNullOrWhiteSpace("FursonaName", user.FursonaName);
            identity.AddClaimIfNotNullOrWhiteSpace("FursonaSpecies", user.FursonaSpecies);
            identity.AddClaimIfNotNullOrWhiteSpace(ClaimTypes.Name, user.IrlFullName);
            identity.AddClaimIfNotNullOrWhiteSpace(ClaimTypes.GivenName, user.IrlShortName);

            if (user.DateOfBirth.HasValue)
            {
                identity.AddClaim(new Claim(ClaimTypes.DateOfBirth, user.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                identity.AddClaim(new Claim("IsOver18", (user.DateOfBirth.Value.AddYears(18) < DateTime.Now) ? "true" : "false"));
            }

            return identity;
        }
    }

    static class IdentityExtensions
    {
        public static void AddClaimIfNotNullOrWhiteSpace(this ClaimsIdentity identity, string claimType, string claimValue)
        {
            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                identity.AddClaim(new Claim(claimType, claimValue));
            }
        }
    }
}