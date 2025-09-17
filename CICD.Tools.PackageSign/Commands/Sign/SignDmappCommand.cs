namespace Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Sign
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.PackageSign;
    using Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Verify;
    using Skyline.DataMiner.CICD.Tools.PackageSign.NuGetSigningAndVerifying;
    using Skyline.DataMiner.CICD.Tools.PackageSign.SystemCommandLine;

    internal class SignDmappCommand : BaseCommand
    {
        public SignDmappCommand() :
            base(name: "dmapp", description: "Signs a DataMiner application (.dmapp) package using a code-signing certificate stored in Azure Key Vault." + Environment.NewLine +
                                             "Environment variables 'AZURE_TENANT_ID', 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_KEY_VAULT_URL' and 'AZURE_KEY_VAULT_CERTIFICATE' can be set or provided via the parameters." + Environment.NewLine +
                                             "This is a Windows-Only command.")
        {
            AddOption(new Option<IDirectoryInfoIO>(
                aliases: ["--output", "-o"],
                description: "Output directory for the signed package(s).",
                parseArgument: OptionHelper.ParseDirectoryInfo!)
            {
                IsRequired = true
            }.LegalFilePathsOnly());
        }
    }

    internal class SignDmappCommandHandler(ILogger<SignDmappCommandHandler> logger, IConfiguration configuration) : BaseCommandHandler
    {
        /*
         * Automatic binding with System.CommandLine.NamingConventionBinder
         * The property names need to match with the command line argument names.
         * Example: --example-package-file will bind to ExamplePackageFile
         */

        public required IDirectoryInfoIO Output { get; set; }

        public override async Task<int> InvokeAsync(InvocationContext context)
        {
            logger.LogDebug($"Starting {nameof(SignDmappCommand)}...");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogError("This command is only supported on Windows.");
                return (int)ExitCodes.InvalidPlatform;
            }
            
            try
            {
                List<IFileInfoIO> packages;
                switch (PackageLocation)
                {
                    case DirectoryInfo directory:
                        packages = new List<IFileInfoIO>(directory.GetFiles("*.dmapp", SearchOption.TopDirectoryOnly));
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

                Output.Create();
                
                bool hadError = false;
                SigningZipVariables variables = new(configuration);
                variables.SetAzureKeyVaultVariables(AzureKeyVaultUri, AzureKeyVaultCertificate);
                variables.SetAzureCredentials(AzureTenantId, AzureClientId, AzureClientSecret);
                foreach (IFileInfoIO packageFile in packages)
                {
                    var result = await SignInternalAsync(variables, packageFile, Output, logger);

                    if (result != (int)ExitCodes.Ok)
                    {
                        hadError = true;
                    }
                }

                return hadError ? (int)ExitCodes.Fail : (int)ExitCodes.Ok;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed the signing of packages.");
                return (int)ExitCodes.UnexpectedException;
            }
            finally
            {
                logger.LogDebug($"Finished {nameof(SignDmappCommand)}.");
            }
        }

        internal static async Task<int> SignInternalAsync(SigningZipVariables variables, IFileInfoIO packageFile, IDirectoryInfoIO outputDirectory, ILogger logger)
        {
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();

            try
            {
                string packageName = FileSystem.Instance.Path.GetFileNameWithoutExtension(packageFile.FullName);
                if (await VerifyDmappCommandHandler.VerifyInternalAsync(variables, packageFile, logger) == (int)ExitCodes.Ok)
                {
                    // Already signed with provided certificate, move to output directory
                    packageFile.CopyTo(FileSystem.Instance.Path.Combine(outputDirectory.FullName, packageFile.Name));
                    return (int)ExitCodes.Ok;
                }

                if (await VerifyDmappCommandHandler.VerifyInternalAsync(variables.WithoutKeyVault(), packageFile, logger) == (int)ExitCodes.Ok)
                {
                    // Already signed with a certificate, move to output directory and throw warning
                    logger.LogWarning("The package '{PackageName}' is already signed with a certificate that does not match with the provided certificate.", packageFile.Name);
                    packageFile.CopyTo(FileSystem.Instance.Path.Combine(outputDirectory.FullName, packageFile.Name));
                    return (int)ExitCodes.Ok;
                }

                SignatureInfo signatureInfo = (await SignatureInfo.GetAsync(variables))!;

                string nupgkFilePath = PackageConverter.ConvertToNupkg(packageFile.FullName, temporaryDirectory);
                PackageConverter.AddNuspecFileToPackage(nupgkFilePath);

                var signer = new NuGetPackageSignerAndVerifier(logger);
                string signedNupkgFilePath = FileSystem.Instance.Path.Combine(temporaryDirectory, packageName + "_Signed.nupkg");
                if (await signer.SignAsync(nupgkFilePath, signedNupkgFilePath, signatureInfo, true))
                {
                    string packageFilePath = PackageConverter.ConvertToPackage(signedNupkgFilePath, outputDirectory.FullName, packageFile.Name);
                    logger.LogDebug("Created signed package at '{PackageFilePath}'", packageFilePath);
                    return (int)ExitCodes.Ok;
                }
                else
                {
                    logger.LogDebug("Failed to sign the package: {PackageName}", packageFile.Name);
                    return (int)ExitCodes.Fail;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Exception during {nameof(SignInternalAsync)}.");
                return (int)ExitCodes.UnexpectedException;
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
            }
        }
    }
}