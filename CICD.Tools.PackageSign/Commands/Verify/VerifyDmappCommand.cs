namespace Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Verify
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.PackageSign;
    using Skyline.DataMiner.CICD.Tools.PackageSign.NuGetSigningAndVerifying;

    internal class VerifyDmappCommand() : BaseCommand(
        name: "dmapp",
        description: "Verifies that a DataMiner application (.dmapp) package is signed." + Environment.NewLine +
                     "Environment variables 'AZURE_TENANT_ID', 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_KEY_VAULT_URL' and 'AZURE_KEY_VAULT_CERTIFICATE' can be set or provided via the parameters." + Environment.NewLine +
                     "If the Azure Key Vault variables are set, the signature is validated against the specified certificate. If not provided, the command only checks if the package is signed, without verifying the owner." + Environment.NewLine +
                     "This is a Windows-Only command.");

    internal class VerifyDmappCommandHandler(ILogger<VerifyDmappCommandHandler> logger, IConfiguration configuration) : BaseCommandHandler
    {
        /*
         * Automatic binding with System.CommandLine.NamingConventionBinder
         * The property names need to match with the command line argument names.
         * Example: --example-package-file will bind to ExamplePackageFile
         */
        
        public override async Task<int> InvokeAsync(InvocationContext context)
        {
            logger.LogDebug($"Starting {nameof(VerifyDmappCommand)}...");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogError("This command is only supported on Windows.");
                return (int)ExitCodes.InvalidPlatform;
            }

            var temporaryDirectory = new DirectoryInfo(FileSystem.Instance.Directory.CreateTemporaryDirectory());

            try
            {
                List<IFileInfoIO> packages;
                switch (PackageLocation)
                {
                    case DirectoryInfo directory:
                        packages = new List<IFileInfoIO>(directory.GetFiles("*.dmapp", SearchOption.AllDirectories));
                        break;
                    case FileInfo file:
                        if (file.Extension != ".dmapp")
                        {
                            return (int)ExitCodes.InvalidFileType;
                        }

                        packages = [file];
                        break;
                    default:
                        logger.LogError("The provided package location is neither a file nor a directory.");
                        return (int)ExitCodes.Fail;
                }

                bool hadError = false;
                SigningZipVariables variables = new(configuration);
                variables.SetAzureKeyVaultVariables(AzureKeyVaultUri, AzureKeyVaultCertificate, required: false);
                variables.SetAzureCredentials(AzureTenantId, AzureClientId, AzureClientSecret, required: variables.HasKeyVaultSet);
                foreach (IFileInfoIO packageFile in packages)
                {
                    var result = await VerifyInternalAsync(variables, packageFile, logger);

                    if (result != (int)ExitCodes.Ok)
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
                logger.LogDebug($"Finished {nameof(VerifyDmappCommand)}.");
            }
        }

        internal static async Task<int> VerifyInternalAsync(SigningZipVariables variables, IFileInfoIO packageFile, ILogger logger)
        {
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();

            try
            {
                SignatureInfo? signatureInfo = await SignatureInfo.GetAsync(variables);

                string nupgkFilePath = PackageConverter.ConvertToNupkg(packageFile.FullName, temporaryDirectory);
                var verifier = new NuGetPackageSignerAndVerifier(logger);
                if (await verifier.VerifyAsync(nupgkFilePath, signatureInfo))
                {
                    logger.LogDebug("Successfully verified the signed package: '{PackageName}'", packageFile.Name);
                    return (int)ExitCodes.Ok;
                }
                else
                {
                    logger.LogDebug("Failed to verify the package: {PackageName}", packageFile.Name);
                    return (int)ExitCodes.Fail;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Exception during {nameof(VerifyInternalAsync)}.");
                return (int)ExitCodes.Fail;
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
            }
        }
    }
}