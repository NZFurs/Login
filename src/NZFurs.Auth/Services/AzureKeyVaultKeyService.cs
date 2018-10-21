using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel;
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
            var header = await CreateHeaderAsync(token);
            var payload = await CreatePayloadAsync(token);

            return await CreateJwtAsync(new JwtSecurityToken(header, payload));
        }

        public async Task<SigningCredentials> GetSigningCredentialsAsync()
        {
            var keyBundle = await _keyVaultClient.GetKeyAsync(_keyVaultClientOptions.KeyVault, _keyVaultClientOptions.KeyName);
            var securityKey = GetSecurityKeyForKeyBundle(keyBundle);

            return new SigningCredentials(securityKey, SecurityAlgorithms.Sha256Digest);
        }

        public async Task<IEnumerable<SecurityKey>> GetValidationKeysAsync()
        {
            // TODO: This is a very expensive operation. We should cache this somehow.

            List<SecurityKey> securityKeys = new List<SecurityKey>();
            var keyItemsPage = await _keyVaultClient.GetKeyVersionsAsync(_keyVaultClientOptions.KeyVault, _keyVaultClientOptions.KeyName);

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
        /// <param name="token">The token.</param>
        /// <returns>The JWT header</returns>
        protected virtual async Task<JwtHeader> CreateHeaderAsync(Token token)
        {
            var credential = await GetSigningCredentialsAsync();

            if (credential == null)
            {
                throw new InvalidOperationException("No signing credential is configured. Can't create JWT token");
            }

            var header = new JwtHeader(credential);

            // emit x5t claim for backwards compatibility with v4 of MS JWT library
            if (credential.Key is X509SecurityKey x509key)
            {
                var cert = x509key.Certificate;
                if (_systemClock.UtcNow.UtcDateTime > cert.NotAfter)
                {
                    _logger.LogWarning("Certificate {subjectName} has expired on {expiration}", cert.Subject, cert.NotAfter.ToString(CultureInfo.InvariantCulture));
                }

                header["x5t"] = Base64Url.Encode(cert.GetCertHash());
            }

            return header;
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
        /// <returns>The signed JWT</returns>
        protected virtual async Task<string> CreateJwtAsync(JwtSecurityToken jwt, CancellationToken cancellationToken = default)
        {
            var keyId = jwt.EncryptingCredentials.Key.KeyId;
            var keyBundle = await _keyVaultClient.GetKeyAsync(keyId, cancellationToken);

            var rawDataBytes = System.Text.Encoding.UTF8.GetBytes(jwt.EncodedHeader + "." + jwt.EncodedPayload); // TODO: Is UTF-8 correct?

            var rawSignature = await _keyVaultClient.SignAsync(keyId, GetAlgorithmForKeyBundle(keyBundle), rawDataBytes, cancellationToken);

            return jwt.EncodedHeader + "." + jwt.EncodedPayload + "." + rawSignature;
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
                    var rsa = keyBundle.Key.ToRSA(false);
                    switch (rsa.KeySize)
                    {
                        case 256:
                            return JsonWebKeySignatureAlgorithm.RS256;
                        case 384:
                            return JsonWebKeySignatureAlgorithm.RS384;
                        case 512:
                            return JsonWebKeySignatureAlgorithm.RS512;
                        default:
                            throw new Exception("RSA Key Vault key uses an unsupported key size.");
                    }
                case JsonWebAlgorithmsKeyTypes.EllipticCurve:
                case "EC-HSM":
                    switch (keyBundle.Key.CurveName)
                    {
                        case JsonWebKeyCurveName.P256:
                            return JsonWebKeySignatureAlgorithm.ES256;
                        case JsonWebKeyCurveName.P384:
                            return JsonWebKeySignatureAlgorithm.ES384;
                        case JsonWebKeyCurveName.P521: // TODO: Is 521 a typo?
                            return JsonWebKeySignatureAlgorithm.ES512;
                        case JsonWebKeyCurveName.P256K:
                            return JsonWebKeySignatureAlgorithm.ES256K;
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
                    return new RsaSecurityKey(rsa);
                case JsonWebAlgorithmsKeyTypes.EllipticCurve:
                case "EC-HSM":
                    var ecdsa = keyBundle.Key.ToECDsa(false);
                    return new ECDsaSecurityKey(ecdsa);
                default:
                    throw new Exception("Key Vault key is an unsupported kty type.");
            }
        }
    }
}
