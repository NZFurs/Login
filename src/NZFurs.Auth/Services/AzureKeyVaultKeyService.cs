using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.KeyVault.WebKey;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.IdentityModel.Tokens;
using NZFurs.Auth.Options;

namespace NZFurs.Auth.Services
{
    public class AzureKeyVaultKeyService : IKeyMaterialService, ITokenCreationService
    {
        private readonly AzureKeyVaultKeyServiceOptions _keyVaultClientOptions;
        private readonly IKeyVaultClient _keyVaultClient;
        private readonly ILogger<AzureKeyVaultKeyService> _logger;
        private readonly ISystemClock _systemClock;

        public AzureKeyVaultKeyService(
            ILogger<AzureKeyVaultKeyService> logger,
            IOptions<AzureKeyVaultKeyServiceOptions> keyVaultKeyServiceOptions,
            ISystemClock systemClock
        )
        {
            // TODO: Check to see required _keyVaultClientOptions are set

            _keyVaultClientOptions = keyVaultKeyServiceOptions.Value;
            _keyVaultClient = new KeyVaultClient(GetAzureActiveDirectoryToken); // TODO: DI this
            _logger = logger;
            _systemClock = systemClock;
        }

        public virtual async Task<string> CreateTokenAsync(Token token)
        {
            var signingCredential = await GetSigningCredentialsAsync();

            var header = await CreateHeaderAsync(signingCredential);
            var payload = await CreatePayloadAsync(token);

            var unsignedJwtSecurityToken = new JwtSecurityToken(header, payload);

            return await CreateJwtAsync(unsignedJwtSecurityToken);
        }

        public async Task<SigningCredentials> GetSigningCredentialsAsync()
        {
            var keyBundle = await _keyVaultClient.GetKeyAsync($"https://{_keyVaultClientOptions.KeyVault}.vault.azure.net", _keyVaultClientOptions.KeyName);

            if (keyBundle == null)
            {
                throw new ApplicationException("No signing key was found in key vault. Can't create JWT token");
            }

            var keyBundleAlgorithm = GetAlgorithmForKeyBundle(keyBundle);
            var securityKey = GetSecurityKeyForKeyBundle(keyBundle);
            securityKey.KeyId = keyBundle.Key.Kid;

            return new SigningCredentials(securityKey, keyBundleAlgorithm);
        }

        public async Task<IEnumerable<SecurityKey>> GetValidationKeysAsync()
        {
            // TODO: This is a very expensive operation. We should cache this somehow.

            List<SecurityKey> securityKeys = new List<SecurityKey>();
            var keyItemsPage = await _keyVaultClient.GetKeyVersionsAsync($"https://{_keyVaultClientOptions.KeyVault}.vault.azure.net", _keyVaultClientOptions.KeyName);

            foreach (var keyItem in keyItemsPage)
            {
                var keyBundle = await _keyVaultClient.GetKeyAsync(keyItem.Kid);
                securityKeys.Add(GetSecurityKeyForKeyBundle(keyBundle));
            }

            while (keyItemsPage.NextPageLink != null)
            {
                keyItemsPage = await _keyVaultClient.GetKeyVersionsNextAsync(keyItemsPage.NextPageLink);

                foreach (var keyItem in keyItemsPage)
                {
                    var keyBundle = await _keyVaultClient.GetKeyAsync(keyItem.Kid);
                    securityKeys.Add(GetSecurityKeyForKeyBundle(keyBundle));
                }
            }

            return securityKeys;
        }

        /// <summary>
        /// Creates the JWT header
        /// </summary>
        /// <param name="signingCredentials"></param>
        /// <returns>The JWT header</returns>
        protected virtual Task<JwtHeader> CreateHeaderAsync(SigningCredentials signingCredentials)
        {
            return Task.FromResult(new JwtHeader(signingCredentials));
        }

        /// <summary>
        /// Creates the JWT payload
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>The JWT payload</returns>
        protected virtual Task<JwtPayload> CreatePayloadAsync(Token token)
        {
            var payload = token.CreateJwtPayload(_systemClock, _logger);
            return Task.FromResult(payload);
        }

        /// <summary>
        /// Applies the signature to the JWT
        /// </summary>
        /// <param name="jwt">The JWT object.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The signed JWT</returns>
        protected virtual async Task<string> CreateJwtAsync(JwtSecurityToken jwt, CancellationToken cancellationToken = default)
        {
            var rawDataBytes = System.Text.Encoding.UTF8.GetBytes(jwt.EncodedHeader + "." + jwt.EncodedPayload); // TODO: Is UTF-8 correct?

            var sigOperationResponse = await _keyVaultClient.SignAsync(jwt.Header.Kid, jwt.Header.Alg, rawDataBytes.Sha256(), cancellationToken);

            var rawSignatureBytes = sigOperationResponse.Result;
            return jwt.EncodedHeader + "." + jwt.EncodedPayload + "." + Base64UrlEncoder.Encode(rawSignatureBytes);
        }

        private async Task<string> GetAzureActiveDirectoryToken(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            foreach (var clientSecret in _keyVaultClientOptions.ClientSecrets)
            {
                ClientCredential clientCred = new ClientCredential(_keyVaultClientOptions.ClientId, clientSecret);
                AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);

                if (result != null) return result.AccessToken;
            }

            throw new InvalidOperationException("Failed to obtain the JWT token");
        }

        protected virtual string GetAlgorithmForKeyBundle(KeyBundle keyBundle)
        {
            switch (keyBundle.Key.Kty)
            {
                case JsonWebAlgorithmsKeyTypes.RSA:
                case "RSA-HSM":
                    // TODO: Support longer digest lengths
                    return JsonWebKeySignatureAlgorithm.RS256;
                case JsonWebAlgorithmsKeyTypes.EllipticCurve:
                case "EC-HSM":
                    switch (keyBundle.Key.CurveName)
                    {
                        case JsonWebKeyCurveName.P256:
                        case JsonWebKeyCurveName.P384:
                        case JsonWebKeyCurveName.P521:
                            return JsonWebKeySignatureAlgorithm.ES256;
                        case JsonWebKeyCurveName.P256K:
                            return JsonWebKeySignatureAlgorithm.ES256K;
                        // TODO: Support longer digest lengths
                        default:
                            throw new Exception("EC Key Vault key uses an unsupported curve.");
                    }
                default:
                    throw new Exception("Key Vault key is an unsupported kty type.");
            }
        }

        protected virtual SecurityKey GetSecurityKeyForKeyBundle(KeyBundle keyBundle)
        {
            switch (keyBundle.Key.Kty)
            {
                case JsonWebAlgorithmsKeyTypes.RSA:
                case "RSA-HSM":
                    var rsa = keyBundle.Key.ToRSA(false);
                    return new RsaSecurityKey(rsa)
                    {
                        KeyId = keyBundle.Key.Kid
                    };
                case JsonWebAlgorithmsKeyTypes.EllipticCurve:
                case "EC-HSM":
                    var ecParameters = new System.Security.Cryptography.ECParameters
                    {
                        Curve = GetECCurveFromJsonWebKey(keyBundle.Key),
                        Q = new ECPoint
                        {
                            X = keyBundle.Key.X,
                            Y = keyBundle.Key.Y,
                        }
                    };
                    var ecdsa = ECDsa.Create(ecParameters);

                    //var ecdsa = keyBundle.Key.ToECDsa(false);
                    return new ECDsaSecurityKey(ecdsa)
                    {
                        KeyId = keyBundle.Key.Kid
                    };
                default:
                    throw new Exception("Key Vault key is an unsupported kty type.");
            }
        }

        protected virtual ECCurve GetECCurveFromJsonWebKey(Microsoft.Azure.KeyVault.WebKey.JsonWebKey jsonWebKey)
        {
            // https://github.com/dotnet/corefx/blob/master/src/System.Security.Cryptography.Algorithms/src/System/Security/Cryptography/ECCurve.NamedCurves.cs#L16-L18
            const string ECDSA_P256_OID_VALUE = "1.2.840.10045.3.1.7"; // nistP256 or secP256r1
            const string ECDSA_P384_OID_VALUE = "1.3.132.0.34"; // nistP384 or secP384r1
            const string ECDSA_P521_OID_VALUE = "1.3.132.0.35"; // nistP521 or secP521r1

            switch (jsonWebKey.CurveName)
            {
                case JsonWebKeyCurveName.P256:
                    return ECCurve.CreateFromOid(new Oid(ECDSA_P256_OID_VALUE));
                case JsonWebKeyCurveName.P384:
                    return ECCurve.CreateFromOid(new Oid(ECDSA_P384_OID_VALUE));
                case JsonWebKeyCurveName.P521: // TODO: Is 521 a typo?
                    return ECCurve.CreateFromOid(new Oid(ECDSA_P521_OID_VALUE));
                case JsonWebKeyCurveName.P256K:
                    throw new NotImplementedException($"Not sure what OID the {JsonWebKeyCurveName.P256K} curve is supposed to be."); // TODO: Research
                default:
                    throw new Exception("EC Key Vault key uses an unsupported curve.");
            }
        }
    }
}
