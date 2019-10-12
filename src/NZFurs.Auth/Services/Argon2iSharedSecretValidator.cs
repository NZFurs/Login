using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using IdentityServer4.Models;
using IdentityServer4.Validation;
using Konscious.Security.Cryptography;

using NZFurs.Auth.Options;

namespace NZFurs.Auth.Services
{
    public class Argon2iSecretValidator : ISecretValidator
    {
        private readonly ILogger<Argon2iSecretValidator> _logger;
        private readonly Argon2iSharedSecretValidatorOptions _options;

        public Argon2iSecretValidator(ILogger<Argon2iSecretValidator> logger, IOptions<Argon2iSharedSecretValidatorOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public Task<SecretValidationResult> ValidateAsync(IEnumerable<Secret> secrets, ParsedSecret parsedSecret)
        {
            if (parsedSecret.Type != "SharedArgon2i")
            {
                _logger.LogDebug("Hashed shared secret validator cannot process {type}", parsedSecret.Type ?? "null");
                return Task.FromResult(new SecretValidationResult { Success = false });
            }

            // Parse user-provided secret
            var parsedSecretInput = Encoding.UTF8.GetBytes(parsedSecret.Credential as string);

            if (parsedSecretInput.Length != 68) // 32 bits + 256 bits + 256 bits
            {
                _logger.LogDebug("Incorrect length for provided secret (expected 68 bytes, got {length})", parsedSecretInput.Length);
                return Task.FromResult(new SecretValidationResult { Success = false });
            }

            // Digest incoming bytes
            byte[] secretId = new byte[4];
            byte[] providedSecretBytes = new byte[32];
            byte[] providedTag = new byte[32];

            Array.Copy(parsedSecretInput, 0, secretId, 0, 4);
            Array.Copy(parsedSecretInput, 4, providedSecretBytes, 0, 32);
            Array.Copy(parsedSecretInput, 35, providedTag, 0, 32);

            // Calculate expected tag value
            HMACBlake2B hMACBlake2B = new HMACBlake2B(_options.ClientSecretHmacKey, 32);
            byte[] messageToDigest = new byte[36];
            Array.Copy(secretId, 0, messageToDigest, 0, 4);
            Array.Copy(providedSecretBytes, 0, messageToDigest, 4, 32);
            byte[] calculatedTag = hMACBlake2B.ComputeHash(messageToDigest);

            if (!ByteArraysEqual(providedTag, calculatedTag))
            {
                _logger.LogDebug("Incorrect HMAC value for supplied secret");
                return Task.FromResult(new SecretValidationResult { Success = false });
            }

            // Get expected secret
            var secretIdString = Convert.ToBase64String(secretId);
            var storedSecret = secrets.FirstOrDefault(s => s.Value.Split(':')[0] == secretIdString);
            var storedSaltBytes = Convert.FromBase64String(storedSecret.Value.Split(':')[1]);
            var storedHashBytes = Convert.FromBase64String(storedSecret.Value.Split(':')[2]);

            var blake2b = new NSec.Cryptography.Blake2b(256);
            byte[] hashMessage = new byte[68];
            Array.Copy(secretId, 0, messageToDigest, 0, 4);
            Array.Copy(storedSaltBytes, 0, messageToDigest, 4, 32);
            Array.Copy(providedSecretBytes, 0, messageToDigest, 4, 32);
            var computedHashBytes = blake2b.Hash(hashMessage);

            if (ByteArraysEqual(computedHashBytes, storedHashBytes))
            {
                return Task.FromResult(new SecretValidationResult { Success = true });
            }
            return Task.FromResult(new SecretValidationResult { Success = false });
        }

        // Compares two byte arrays for equality. The method is specifically written so that the loop is not optimized.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null && b == null)
            {
                return true;
            }
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }
            var areSame = true;
            for (var i = 0; i < a.Length; i++)
            {
                areSame &= (a[i] == b[i]);
            }
            return areSame;
        }
    }
}
