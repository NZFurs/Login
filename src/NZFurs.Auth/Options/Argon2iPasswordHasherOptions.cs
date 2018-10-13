using System;

namespace NZFurs.Auth.Options
{
    public class Argon2iPasswordHasherOptions
    {
        public int DegreeOfParallelism { get; set; }
        public int Iterations { get; set; }
        public string KnownSecretBase64 { get; set; }
        public int MemorySize { get; set; }
        public int HashSize { get; set; }
        public int SaltSize { get; set; }
        public byte[] KnownSecret
        {
            get
            {
                return Convert.FromBase64String(KnownSecretBase64);
            }
        }
    }
}