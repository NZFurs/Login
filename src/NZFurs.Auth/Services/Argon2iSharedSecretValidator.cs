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

using NZFurs.Auth.Options;
using NSec.Cryptography;

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
            if (parsedSecret.Type != "SharedSecret")
            {
                _logger.LogDebug("Hashed shared secret validator cannot process {type}", parsedSecret.Type ?? "null");
                return Task.FromResult(new SecretValidationResult { Success = false });
            }

            // Parse user-provided secret
            var parsedSecretInput = Convert.FromBase64String(parsedSecret.Credential as string);

            if (parsedSecretInput.Length != 70) // 40 bits + 256 bits + 256 bits
            {
                _logger.LogDebug("Incorrect length for provided secret (expected 70 bytes, got {length})", parsedSecretInput.Length);
                return Task.FromResult(new SecretValidationResult { Success = false });
            }

            // Digest incoming bytes
            var secretId = new byte[6];
            var providedSecretBytes = new byte[32];
            var providedTag = new byte[32];
            Array.Copy(parsedSecretInput, 0, secretId, 0, 6);
            Array.Copy(parsedSecretInput, 6, providedSecretBytes, 0, 32);
            Array.Copy(parsedSecretInput, 38, providedTag, 0, 32);

            // Calculate expected tag value
            var messageToDigest = new byte[38];
            Array.Copy(secretId, 0, messageToDigest, 0, 6);
            Array.Copy(providedSecretBytes, 0, messageToDigest, 6, 32);
            var blake2bMac = new Blake2bMac(32, 32);
            var nsecMacKey = Key.Import(blake2bMac, _options.ClientSecretHmacKey, KeyBlobFormat.RawSymmetricKey);
            var calculatedTag = blake2bMac.Mac(nsecMacKey, messageToDigest);

            if (!ByteArraysEqual(providedTag, calculatedTag))
            {
                _logger.LogDebug("Incorrect HMAC value for supplied secret");
                return Task.FromResult(new SecretValidationResult { Success = false });
            }

            // Get expected secret
            var storedSecret = secrets.FirstOrDefault(s => Convert.FromBase64String(s.Value).Take(6).SequenceEqual(secretId));
            var storedSecretBytes = Convert.FromBase64String(storedSecret.Value);
            var storedSaltBytes = new byte[32];
            var storedHashBytes = new byte[32];
            Array.Copy(storedSecretBytes, 6, storedSaltBytes, 0, 32);
            Array.Copy(storedSecretBytes, 38, storedHashBytes, 0, 32);

            var blake2b = new Blake2b(32);
            var hashMessage = new byte[70];
            Array.Copy(secretId, 0, hashMessage, 0, 6);
            Array.Copy(storedSaltBytes, 0, hashMessage, 6, 32);
            Array.Copy(providedSecretBytes, 0, hashMessage, 38, 32);
            var computedHashBytes = blake2b.Hash(hashMessage);

            if (!ByteArraysEqual(computedHashBytes, storedHashBytes))
            {
                _logger.LogDebug("Incorrect HMAC value for supplied secret");
                return Task.FromResult(new SecretValidationResult { Success = false });
            }
            return Task.FromResult(new SecretValidationResult { Success = true });
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
