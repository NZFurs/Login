using System.Collections.Generic;

namespace NZFurs.Auth.Options
{
    public class AzureKeyVaultKeyServiceOptions
    {
        public string ClientId { get; set; }
        public IEnumerable<string> ClientSecrets { get; set; }
        public string KeyVault { get; set; }
        public string KeyName { get; set; }
    }
}
