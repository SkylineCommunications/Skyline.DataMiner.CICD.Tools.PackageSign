﻿// Modified from dotnet/sign (https://github.com/dotnet/sign)
// Original License: MIT (See the LICENSE.MIT file in this directory for more information)
namespace Skyline.DataMiner.CICD.Tools.PackageSign.NuGetSigningAndVerifying
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using NuGet.Packaging;
    using NuGet.Packaging.Signing;

    using Skyline.DataMiner.CICD.FileSystem;

    using HashAlgorithmName = NuGet.Common.HashAlgorithmName;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    internal sealed class NuGetPackageSignerAndVerifier
    {
        private readonly ILogger _logger;

        public NuGetPackageSignerAndVerifier(ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            _logger = logger;
        }

        public async Task<bool> SignAsync(string packagePath, string outputPath, SignatureInfo signatureInfo, bool overwrite, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(packagePath, nameof(packagePath));
            ArgumentException.ThrowIfNullOrEmpty(outputPath, nameof(outputPath));
            ArgumentNullException.ThrowIfNull(signatureInfo, nameof(signatureInfo));

            NuGetSignatureProvider signatureProvider = new(signatureInfo.Rsa, new Rfc3161TimestampProvider(new Uri("http://timestamp.acs.microsoft.com")));
            SignPackageRequest request = new AuthorSignPackageRequest(signatureInfo.Certificate, HashAlgorithmName.SHA256);

            string packageFileName = FileSystem.Instance.Path.GetFileName(packagePath);

            _logger.LogDebug($"{nameof(SignAsync)} [{packagePath}]: Begin signing {packageFileName}");

            try
            {
                using SigningOptions options = SigningOptions.CreateFromFilePaths(
                    packagePath,
                    outputPath,
                    overwrite,
                    signatureProvider,
                    new NuGetLogger(_logger, packagePath));
                await SigningUtility.SignAsync(options, request, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                return false;
            }
            finally
            {
                _logger.LogDebug($"{nameof(SignAsync)} [{packagePath}]: End signing {packageFileName}");
            }

            return true;
        }

        public async Task<bool> VerifyAsync(string packagePath, SignatureInfo signatureInfo)
        {
            ArgumentException.ThrowIfNullOrEmpty(packagePath, nameof(packagePath));

            string packageFileName = FileSystem.Instance.Path.GetFileName(packagePath);

            _logger.LogDebug($"{nameof(VerifyAsync)} [{packagePath}]: Begin verifying {packageFileName}");

            try
            {
                List<ISignatureVerificationProvider> signatureVerificationProviders =
                [
                    new IntegrityVerificationProvider(), // Check if package is tampered
                    new SignatureTrustAndValidityVerificationProvider(), // Check if valid signature
                ];

                if (signatureInfo != null)
                {
                    // Signature info is provided, add certificate to allow list so only this certificate is valid.
                    var certificateFingerprintString = CertificateUtility.GetHashString(signatureInfo.Certificate, HashAlgorithmName.SHA256);
                    AllowListVerificationProvider allowListVerificationProvider = new AllowListVerificationProvider(new List<VerificationAllowListEntry>
                    {
                        new CertificateHashAllowListEntry(VerificationTarget.Author, SignaturePlacement.PrimarySignature, certificateFingerprintString, HashAlgorithmName.SHA256),
                    });

                    signatureVerificationProviders.Add(allowListVerificationProvider);
                }

                PackageSignatureVerifier packageSignatureVerifier = new PackageSignatureVerifier(signatureVerificationProviders);

                using var packageReader = new PackageArchiveReader(packagePath);
                var result = await packageSignatureVerifier.VerifySignaturesAsync(packageReader, SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(), CancellationToken.None);

                _logger.LogDebug($"{nameof(VerifyAsync)} [{packagePath}]: Verified {packageFileName}. IsSigned: {result.IsSigned} IsValid: {result.IsValid}");
                return result.IsSigned && result.IsValid;
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                return false;
            }
            finally
            {
                _logger.LogDebug($"{nameof(VerifyAsync)} [{packagePath}]: End verifying {packageFileName}");
            }
        }
    }
}
