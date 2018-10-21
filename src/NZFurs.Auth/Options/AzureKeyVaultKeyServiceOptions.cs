namespace NZFurs.Auth.Options
{
    public class AzureKeyVaultKeyServiceOptions
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string KeyVault { get; set; }
        public string KeyName { get; set; }
    }
}
