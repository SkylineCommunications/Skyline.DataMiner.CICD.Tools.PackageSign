namespace Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Verify
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.PackageSign;
    using Skyline.DataMiner.CICD.Tools.PackageSign.NuGetSigningAndVerifying;

    internal class VerifyDmappCommand : BaseCommand
    {
        public VerifyDmappCommand() :
            base(name: "dmapp", description: "Verifies that a DataMiner application (.dmapp) package is signed. If the environment variables AZURE_TENANT_ID, AZURE_CLIENT_ID, and AZURE_CLIENT_SECRET are set, the signature is validated against the specified certificate. If not provided, the command only checks if the package is signed, without verifying the owner.")
        {
            AddOption(new Option<Uri>(
                aliases: ["--azure-key-vault-url", "-kvu"],
                description: "URL to an Azure Key Vault."));

            AddOption(new Option<string>(
                aliases: ["--azure-key-vault-certificate", "-kvc"],
                description: "Name of the certificate in Azure Key Vault."));
        }
    }

    internal class VerifyDmappCommandHandler(ILogger<VerifyDmappCommandHandler> logger, IConfiguration configuration) : BaseCommandHandler
    {
        /*
         * Automatic binding with System.CommandLine.NamingConventionBinder
         * The property names need to match with the command line argument names.
         * Example: --example-package-file will bind to ExamplePackageFile
         */

        public Uri? AzureKeyVaultUri { get; set; }

        public string? AzureKeyVaultCertificate { get; set; }
        
        public override async Task<int> InvokeAsync(InvocationContext context)
        {
            logger.LogDebug("Starting {Method}...", nameof(VerifyDmappCommand));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogError("This command is only supported on Windows.");
                return (int)ExitCodes.InvalidPlatform;
            }

            var temporaryDirectory = new DirectoryInfo(FileSystem.Instance.Directory.CreateTemporaryDirectory());

            try
            {
                List<IFileInfoIO> packages;
                if (PackageLocation is DirectoryInfo directory)
                {
                    packages = new List<IFileInfoIO>(directory.GetFiles("*.dmapp", SearchOption.TopDirectoryOnly));
                }
                else if (PackageLocation is FileInfo file)
                {
                    packages = [file];
                }
                else
                {
                    logger.LogError("The provided package location is neither a file nor a directory.");
                    return (int)ExitCodes.Fail;
                }

                bool hadError = false;
                foreach (IFileInfoIO packageFile in packages)
                {
                    var result = await VerifyInternalAsync(configuration, packageFile, AzureKeyVaultCertificate, AzureKeyVaultUri, logger);

                    if (result != 0)
                    {
                        hadError = true;
                    }
                }

                return hadError ? (int)ExitCodes.Fail : (int)ExitCodes.Ok;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed the verification of the packages.");
                return (int)ExitCodes.UnexpectedException;
            }
            finally
            {
                temporaryDirectory.Delete(true);
                logger.LogDebug("Finished {Method}.", nameof(VerifyDmappCommand));
            }
        }

        internal static async Task<int> VerifyInternalAsync(IConfiguration configuration, IFileInfoIO packageFile, string? certificateId, Uri? url, ILogger logger)
        {
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();

            try
            {
                SignatureInfo? signatureInfo = null;
                if (!String.IsNullOrWhiteSpace(certificateId) && url != null)
                {
                    // Certificate has been passed along, so verifying against the provided certificate.
                    signatureInfo = await SignatureInfo.GetAsync(configuration, certificateId, url);
                }

                string nupgkFilePath = DmappConverter.ConvertToNupgk(packageFile.FullName, temporaryDirectory);
                var verifier = new NuGetPackageSignerAndVerifier(logger);
                if (await verifier.VerifyAsync(nupgkFilePath, signatureInfo))
                {
                    logger.LogDebug("Successfully verified the signed dmapp package: '{PackageLocation}'", packageFile.FullName);
                    return (int)ExitCodes.Ok;
                }
                else
                {
                    logger.LogDebug("Failed to verify the package: {PackageLocation}", packageFile.FullName);
                    return (int)ExitCodes.Fail;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception during VerifyInternalAsync Run");
                return (int)ExitCodes.Fail;
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
            }
        }
    }
}