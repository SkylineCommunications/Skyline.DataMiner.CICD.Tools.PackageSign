namespace Skyline.DataMiner.CICD.Tools.PackageSign
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    using Azure.Identity;
    using Azure.Security.KeyVault.Certificates;

    using Azure.Security.KeyVault.Keys.Cryptography;

    using Microsoft.Extensions.Configuration;

    using NuGet.Packaging.Signing;

    internal class SignatureInfo
    {
        public RSA Rsa { get; private init; }

        public X509Certificate2 Certificate { get; private init; }

        public static async Task<SignatureInfo> GetAsync(IConfiguration configuration, string certificateId, Uri url)
        {
            string tenantId = configuration["AZURE_TENANT_ID"];
            string clientId = configuration["AZURE_CLIENT_ID"];
            string clientSecret = configuration["AZURE_CLIENT_SECRET"];

            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

            var certificateClient = new CertificateClient(url, credential);
            var certificate = await certificateClient.GetCertificateAsync(certificateId);

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
            return new SignatureInfo
            {
                Certificate = x509Certificate2,
                Rsa = rsa
            };
        }
    }
}