namespace Skyline.DataMiner.CICD.Tools.PackageSign
{
    internal class SigningZipVariables
    {
        private readonly IConfiguration _configuration;

        public SigningZipVariables(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SigningZipVariables(string? azureTenantId, string? azureClientId, string? azureClientSecret)
        {
            AzureTenantId = azureTenantId;
            AzureClientId = azureClientId;
            AzureClientSecret = azureClientSecret;
        }

        public Uri? AzureKeyVaultUri { get; private set; }

        public string? AzureKeyVaultCertificate { get; private set; }

        public string? AzureTenantId { get; private set; }

        public string? AzureClientId { get; private set; }

        public string? AzureClientSecret { get; private set; }

        public bool HasKeyVaultSet { get; private set; }

        public void SetAzureKeyVaultVariables(Uri? azureKeyVaultUri = null, string? azureKeyVaultCertificate = null, bool required = true)
        {
            if (azureKeyVaultUri == null)
            {
                var urlString = _configuration["AZURE_KEY_VAULT_URL"];
                if (required && String.IsNullOrWhiteSpace(urlString))
                {
                    throw new ArgumentException("Azure Key Vault URL is not provided. Please set the environment variable AZURE_KEY_VAULT_URL or provide it as a parameter.");
                }

                if (!Uri.TryCreate(urlString, UriKind.Absolute, out azureKeyVaultUri))
                {
                    throw new ArgumentException("Azure Key Vault URL is invalid.");
                }
            }

            AzureKeyVaultUri = azureKeyVaultUri;

            if (String.IsNullOrWhiteSpace(azureKeyVaultCertificate))
            {
                azureKeyVaultCertificate = _configuration["AZURE_KEY_VAULT_CERTIFICATE"];

                if (required && String.IsNullOrWhiteSpace(azureKeyVaultCertificate))
                {
                    throw new ArgumentException("Azure Key Vault certificate name is not provided. Please set the environment variable AZURE_KEY_VAULT_CERTIFICATE or provide it as a parameter.");
                }
            }

            AzureKeyVaultCertificate = azureKeyVaultCertificate;

            HasKeyVaultSet = AzureKeyVaultUri != null && !String.IsNullOrWhiteSpace(AzureKeyVaultCertificate);
        }

        public void SetAzureCredentials(string? tenantId = null, string? clientId = null, string? clientSecret = null, bool required = true)
        {
            if (String.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = _configuration["AZURE_TENANT_ID"];
            }

            if (String.IsNullOrWhiteSpace(clientId))
            {
                clientId = _configuration["AZURE_CLIENT_ID"];
            }

            if (String.IsNullOrWhiteSpace(clientSecret))
            {
                clientSecret = _configuration["AZURE_CLIENT_SECRET"];
            }

            if (required && (String.IsNullOrWhiteSpace(tenantId) || String.IsNullOrWhiteSpace(clientId) || String.IsNullOrWhiteSpace(clientSecret)))
            {
                throw new ArgumentException("Azure Key Vault credentials are not fully provided. Please set the environment variables AZURE_TENANT_ID, AZURE_CLIENT_ID, and AZURE_CLIENT_SECRET or provide them as parameters.");
            }

            AzureTenantId = tenantId;
            AzureClientId = clientId;
            AzureClientSecret = clientSecret;
        }

        public SigningZipVariables WithoutKeyVault()
        {
            return new SigningZipVariables(AzureTenantId, AzureClientId, AzureClientSecret);
        }
    }

    internal class SigningProtocolVariables(IConfiguration configuration) : SigningZipVariables(configuration)
    {
        public string? Domain { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public void SetProtocolSigningCredentials(string? domain = null, string? username = null, string? password = null)
        {
            if (String.IsNullOrWhiteSpace(domain))
            {
                domain = configuration["SIGNING_DOMAIN"];
            }

            if (String.IsNullOrWhiteSpace(username))
            {
                username = configuration["SIGNING_USERNAME"];
            }

            if (String.IsNullOrWhiteSpace(password))
            {
                password = configuration["SIGNING_PASSWORD"];
            }

            if (String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Protocol signing credentials are not fully provided. Please set the environment variables SIGNING_USERNAME and SIGNING_PASSWORD or provide them as parameters.");
            }

            Domain = domain;
            Username = username;
            Password = password;
        }
    }
}