using System;
using System.Security.Cryptography;

using NSec.Cryptography;

namespace NZFurs.Auth.ClientSecretGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1 || String.IsNullOrWhiteSpace(args[0])) throw new Exception("Please supply the protection key.");

            var secretId = new byte[6];
            var secretBytes = new byte[32];
            var salt = new byte[32];
            var hmacKeyBytes = Convert.FromBase64String(args[0]);

            using (var csprng = RandomNumberGenerator.Create())
            {
                csprng.GetBytes(secretId);
                csprng.GetBytes(secretBytes);
                csprng.GetBytes(salt);
            }
            
            // Calculate tag value
            byte[] messageToDigest = new byte[38];
            Array.Copy(secretId, 0, messageToDigest, 0, 6);
            Array.Copy(secretBytes, 0, messageToDigest, 6, 32);
            var blake2bMac = new Blake2bMac(32, 32);
            var nsecMacKey = Key.Import(blake2bMac, hmacKeyBytes, KeyBlobFormat.RawSymmetricKey);
            var hmacTag = blake2bMac.Mac(nsecMacKey, messageToDigest);

            // Calculate hash
            var blake2b = new Blake2b(32);
            byte[] hashMessage = new byte[70];
            Array.Copy(secretId, 0, hashMessage, 0, 6);
            Array.Copy(salt, 0, hashMessage, 6, 32);
            Array.Copy(secretBytes, 0, hashMessage, 38, 32);
            var hash = blake2b.Hash(hashMessage);

            // Final Prep
            var clientSecretBytes = new byte[70];
            Array.Copy(secretId, 0, clientSecretBytes, 0, 6);
            Array.Copy(secretBytes, 0, clientSecretBytes, 6, 32);
            Array.Copy(hmacTag, 0, clientSecretBytes, 38, 32);

            // Final Prep
            var secretStroageBytes = new byte[70];
            Array.Copy(secretId, 0, secretStroageBytes, 0, 6);
            Array.Copy(salt, 0, secretStroageBytes, 6, 32);
            Array.Copy(hash, 0, secretStroageBytes, 38, 32);

            Console.WriteLine($"Provide to client: {Convert.ToBase64String(clientSecretBytes)}");
            Console.WriteLine($"Store in database: {Convert.ToBase64String(secretStroageBytes)}");
            Console.ReadLine();
        }
    }
}
