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
            base(name: "dmapp", description: "Signs a DataMiner application (.dmapp) package using a code-signing certificate stored in Azure Key Vault. Requires the environment variables AZURE_TENANT_ID, AZURE_CLIENT_ID, and AZURE_CLIENT_SECRET for authentication. This is a Windows-Only command.")
        {
            AddOption(new Option<Uri>(
                aliases: ["--azure-key-vault-url", "-kvu"],
                description: "URL to an Azure Key Vault.")
            {
                IsRequired = true
            });

            AddOption(new Option<string>(
                aliases: ["--azure-key-vault-certificate", "-kvc"],
                description: "Name of the certificate in Azure Key Vault.")
            {
                IsRequired = true
            });

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

        public required Uri AzureKeyVaultUri { get; set; }

        public required string AzureKeyVaultCertificate { get; set; }

        public required IDirectoryInfoIO Output { get; set; }

        public override async Task<int> InvokeAsync(InvocationContext context)
        {
            logger.LogDebug("Starting {Method}...", nameof(SignDmappCommand));

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
                        packages = [file];
                        break;
                    default:
                        logger.LogError("The provided package location is neither a file nor a directory.");
                        return (int)ExitCodes.Fail;
                }

                Output.Create();
                
                bool hadError = false;
                foreach (IFileInfoIO packageFile in packages)
                {
                    var result = await SignInternalAsync(configuration, packageFile, AzureKeyVaultCertificate, AzureKeyVaultUri, Output, logger);

                    if (result == 0)
                    {
                        logger.LogInformation("Created signed dmapp package '{PackageFileName}' at '{OutputFullName}'", packageFile.Name, Output.FullName);
                    }
                    else
                    {
                        logger.LogError("Failed to sign the package: {PackageFileFullName}", packageFile.FullName);
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
                logger.LogDebug("Finished {Method}.", nameof(SignDmappCommand));
            }
        }

        internal static async Task<int> SignInternalAsync(IConfiguration configuration, IFileInfoIO packageFile, string certificateId, Uri url, IDirectoryInfoIO outputDirectory, ILogger logger)
        {
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();

            try
            {
                string packageName = FileSystem.Instance.Path.GetFileNameWithoutExtension(packageFile.FullName);
                if (await VerifyDmappCommandHandler.VerifyInternalAsync(configuration, packageFile, certificateId, url, logger) == (int)ExitCodes.Ok)
                {
                    // Already signed with provided certificate, move to output directory
                    packageFile.CopyTo(FileSystem.Instance.Path.Combine(outputDirectory.FullName, $"{packageName}.dmapp"));
                    return (int)ExitCodes.Ok;
                }

                if (await VerifyDmappCommandHandler.VerifyInternalAsync(configuration, packageFile, null, null, logger) == (int)ExitCodes.Ok)
                {
                    // Already signed with a certificate, move to output directory and throw warning
                    logger.LogWarning("The package '{PackageLocation}' is already signed with a certificate that does not match with the provided certificate.", packageFile.FullName);
                    packageFile.CopyTo(FileSystem.Instance.Path.Combine(outputDirectory.FullName, $"{packageName}.dmapp"));
                    return (int)ExitCodes.Ok;
                }

                // TODO: See if it's worth it to add a check if the package already has a nuspec file (previous signing that went wrong or trying to resign with different certificate)

                SignatureInfo signatureInfo = await SignatureInfo.GetAsync(configuration, certificateId, url);

                string nupgkFilePath = DmappConverter.ConvertToNupgk(packageFile.FullName, temporaryDirectory);
                DmappConverter.AddNuspecFileToPackage(nupgkFilePath);

                var signer = new NuGetPackageSignerAndVerifier(logger);
                string signedNupkgFilePath = FileSystem.Instance.Path.Combine(temporaryDirectory, packageName + "_Signed.nupkg");
                if (await signer.SignAsync(nupgkFilePath, signedNupkgFilePath, signatureInfo, true))
                {
                    string dmappFilePath = DmappConverter.ConvertToDmapp(signedNupkgFilePath, outputDirectory.FullName, packageName);
                    logger.LogDebug("Created signed dmapp package at '{DmappFilePath}'", dmappFilePath);
                    return (int)ExitCodes.Ok;
                }
                else
                {
                    logger.LogDebug("Failed to sign the package: {S}", packageFile.FullName);
                    return (int)ExitCodes.Fail;
                }
            }
            catch (Exception e)
            {
                logger.LogError("Exception during SignInternalAsync Run: {Exception}", e);
                return (int)ExitCodes.UnexpectedException;
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
            }
        }
    }
}