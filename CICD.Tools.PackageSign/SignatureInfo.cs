namespace Skyline.DataMiner.CICD.Tools.PackageSign
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    using Azure.Identity;
    using Azure.Security.KeyVault.Certificates;

    using Azure.Security.KeyVault.Keys.Cryptography;

    using NuGet.Packaging.Signing;

    internal class SignatureInfo
    {
        private SignatureInfo(RSA rsa, X509Certificate2 certificate)
        {
            Rsa = rsa;
            Certificate = certificate;
        }

        public RSA Rsa { get; private init; }

        public X509Certificate2 Certificate { get; private init; }

        public static async Task<SignatureInfo?> GetAsync(SigningZipVariables variables)
        {
            if (variables.AzureKeyVaultUri == null || String.IsNullOrWhiteSpace(variables.AzureKeyVaultCertificate))
            {
                return null;
            }
            
            var credential = new ClientSecretCredential(variables.AzureTenantId, variables.AzureClientId, variables.AzureClientSecret);

            var certificateClient = new CertificateClient(variables.AzureKeyVaultUri, credential);
            var certificate = await certificateClient.GetCertificateAsync(variables.AzureKeyVaultCertificate);

            var keyId = certificate.Value.KeyId;
            if (keyId == null)
            {
                throw new SignatureException("The certificate does not have an associated key.");
            }

            // Create Cryptography Client to sign data
            var cryptoClient = new CryptographyClient(keyId, credential);

            X509Certificate2 x509Certificate2 = new X509Certificate2(certificate.Value.Cer);

            DateTime now = DateTime.Now;
            if (now < x509Certificate2.NotBefore)
            {
                throw new SignatureException("Certificate is not yet time valid.");
            }

            if (x509Certificate2.NotAfter < now)
            {
                throw new SignatureException("Certificate is expired.");
            }

            var rsa = await cryptoClient.CreateRSAAsync();
            return new SignatureInfo(rsa, x509Certificate2);
        }
    }
}