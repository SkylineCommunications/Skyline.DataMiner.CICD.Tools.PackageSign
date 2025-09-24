namespace Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Sign
{
    using System;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;

    using ProtocolSigningService;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.PackageSign;
    using Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Verify;
    using Skyline.DataMiner.CICD.Tools.PackageSign.NuGetSigningAndVerifying;
    using Skyline.DataMiner.CICD.Tools.PackageSign.SystemCommandLine;

    internal class SignDmprotocolCommand : BaseCommand
    {
        public SignDmprotocolCommand() :
            base(name: "dmprotocol", description: "Signs a DataMiner protocol (.dmprotocol) package using a code-signing certificate stored in Azure Key Vault and the XML via the Protocol Signing Service." + Environment.NewLine +
                                                  "Environment variables 'AZURE_TENANT_ID', 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_KEY_VAULT_URL', 'AZURE_KEY_VAULT_CERTIFICATE', 'SIGNING_DOMAIN', 'SIGNING_USERNAME' and 'SIGNING_PASSWORD' can be set or provided via the parameters." + Environment.NewLine +
                                                  "This is a Windows-Only command.")
        {
            AddOption(new Option<string?>(
                aliases: ["--domain", "-d"],
                description: "Domain of the account to connect to the Protocol Signing Service."));

            AddOption(new Option<string?>(
                aliases: ["--username", "-u"],
                description: "Username of the account to connect to the Protocol Signing Service."));

            AddOption(new Option<string?>(
                aliases: ["--password", "-p"],
                description: "Password of the account to connect to the Protocol Signing Service."));

            AddOption(new Option<IDirectoryInfoIO?>(
                aliases: ["--output", "-o"],
                description: "Output directory for the signed package(s). If not provided, it will overwrite the provided file(s).",
                parseArgument: OptionHelper.ParseDirectoryInfo).LegalFilePathsOnly());
        }
    }

    internal class SignDmprotocolCommandHandler(ILogger<SignDmprotocolCommandHandler> logger, IConfiguration configuration) : BaseCommandHandler
    {
        /*
         * Automatic binding with System.CommandLine.NamingConventionBinder
         * The property names need to match with the command line argument names.
         * Example: --example-package-file will bind to ExamplePackageFile
         */

        public string? Domain { get; set; }

        public string? Username { get; set; }

        public string? Password { get; set; }

        public IDirectoryInfoIO? Output { get; set; }

        public override async Task<int> InvokeAsync(InvocationContext context)
        {
            logger.LogDebug($"Starting {nameof(SignDmprotocolCommand)}...");

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
                        packages = new List<IFileInfoIO>(directory.GetFiles("*.dmprotocol", SearchOption.AllDirectories));
                        break;
                    case FileInfo file:
                        if (file.Extension != ".dmprotocol")
                        {
                            return (int)ExitCodes.InvalidFileType;
                        }

                        packages = [file];
                        break;
                    default:
                        logger.LogError("The provided package location is neither a file nor a directory.");
                        return (int)ExitCodes.Fail;
                }

                Output?.Create();

                bool hadError = false;
                SigningProtocolVariables variables = new(configuration);
                variables.SetAzureKeyVaultVariables(AzureKeyVaultUri, AzureKeyVaultCertificate);
                variables.SetAzureCredentials(AzureTenantId, AzureClientId, AzureClientSecret);
                variables.SetProtocolSigningCredentials(Domain, Username, Password);
                await using (SoapSoapClient client = new SoapSoapClient(SoapSoapClient.EndpointConfiguration.SoapSoap))
                {
                    var connectionGuid = (await client.ConnectAsync(variables.Username, variables.Password, variables.Domain))?.Body?.ConnectResult;

                    if (String.IsNullOrWhiteSpace(connectionGuid))
                    {
                        logger.LogError("Invalid Authentication user ({Username}) for Skyline Signing Service: https://protocol.skyline.be", variables.Username);
                        return (int)ExitCodes.Fail;
                    }
                    
                    foreach (IFileInfoIO packageFile in packages)
                    {
                        if (!await SignProtocolPackageAsync(packageFile, client, connectionGuid, variables))
                        {
                            hadError = true;
                        }
                    }

                    await client.LogOutAsync(connectionGuid);
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
                logger.LogDebug($"Finished {nameof(SignDmprotocolCommand)}.");
            }
        }

        private async Task<bool> SignProtocolPackageAsync(IFileInfoIO packageFile, SoapSoapClient client, string connectionGuid, SigningProtocolVariables variables)
        {
            var tempDir = FileSystem.Instance.Directory.CreateTemporaryDirectory();

            try
            {
                string unzipDirectory = FileSystem.Instance.Path.Combine(tempDir, "Unzip");

                // Unzip package to temporary directory
                using (ZipArchive archive = ZipFile.Open(packageFile.FullName, ZipArchiveMode.Read))
                {
                    archive.ExtractToDirectory(unzipDirectory);
                }

                // Find protocol.xml in unzipped directory
                var protocolXmlPath = FileSystem.Instance.Path.Combine(unzipDirectory, "Protocol", "Protocol.xml");
                if (!FileSystem.Instance.File.Exists(protocolXmlPath))
                {
                    logger.LogError("Could not find Protocol.xml in the package {PackageName}.", packageFile.Name);
                    return false;
                }

                // Sign protocol.xml
                int protocolXmlSignResult = await SignProtocolXmlAsync(client, connectionGuid, protocolXmlPath, logger);
                if (protocolXmlSignResult != (int)ExitCodes.Ok)
                {
                    return false;
                }
                            
                // Zip back as a package
                string signedProtocolXmlPackageFilePath = FileSystem.Instance.Path.Combine(tempDir, packageFile.Name);
                ZipFile.CreateFromDirectory(unzipDirectory, signedProtocolXmlPackageFilePath);

                // Sign package
                int protocolPackageSignResult = await SignProtocolPackageInternalAsync(variables, new FileInfo(signedProtocolXmlPackageFilePath), Output, logger);
                return protocolPackageSignResult == (int)ExitCodes.Ok;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unexpected exception during signing of package {PackageName}.", packageFile.Name);
                return false;
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(tempDir, true);
            }
        }

        internal static async Task<int> SignProtocolPackageInternalAsync(SigningZipVariables variables, IFileInfoIO packageFile, IDirectoryInfoIO? outputDirectory, ILogger logger)
        {
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();

            try
            {
                string packageName = FileSystem.Instance.Path.GetFileNameWithoutExtension(packageFile.FullName);
                if (await VerifyDmprotocolCommandHandler.VerifyInternalAsync(variables, packageFile, logger) == (int)ExitCodes.Ok)
                {
                    if (outputDirectory != null)
                    {
                        // Already signed with provided certificate, move to output directory
                        packageFile.CopyTo(FileSystem.Instance.Path.Combine(outputDirectory.FullName, packageFile.Name));
                    }

                    return (int)ExitCodes.Ok;
                }

                if (await VerifyDmprotocolCommandHandler.VerifyInternalAsync(variables.WithoutKeyVault(), packageFile, logger) == (int)ExitCodes.Ok)
                {
                    if (outputDirectory != null)
                    {
                        // Already signed with a certificate, move to output directory and throw warning
                        logger.LogWarning("The package '{PackageName}' is already signed with a certificate that does not match with the provided certificate.", packageFile.Name);
                        packageFile.CopyTo(FileSystem.Instance.Path.Combine(outputDirectory.FullName, packageFile.Name));
                    }

                    return (int)ExitCodes.Ok;
                }

                SignatureInfo? signatureInfo = await SignatureInfo.GetAsync(variables);
                if (signatureInfo == null)
                {
                    logger.LogError("Failed to retrieve signature information from Key Vault. Please ensure all required variables are set.");
                    return (int)ExitCodes.Fail;
                }

                string nupgkFilePath = PackageConverter.ConvertToNupkg(packageFile.FullName, temporaryDirectory);
                PackageConverter.AddNuspecFileToPackage(nupgkFilePath);

                var signer = new NuGetPackageSignerAndVerifier(logger);
                string signedNupkgFilePath = FileSystem.Instance.Path.Combine(temporaryDirectory, packageName + "_Signed.nupkg");
                if (await signer.SignAsync(nupgkFilePath, signedNupkgFilePath, signatureInfo, true))
                {
                    string packageFilePath = PackageConverter.ConvertToPackage(signedNupkgFilePath, outputDirectory?.FullName ?? packageFile.DirectoryName, packageFile.Name);
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
                logger.LogError(e, $"Exception during {nameof(SignProtocolPackageInternalAsync)}.");
                return (int)ExitCodes.UnexpectedException;
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
            }
        }

        internal static async Task<int> SignProtocolXmlAsync(SoapSoapClient client, string connectionGuid, string xmlFile, ILogger logger)
        {
            try
            {
                SignProtocolXmlFileResponse response = await client.SignProtocolXmlFileAsync(connectionGuid, FileSystem.Instance.File.ReadAllBytes(xmlFile));
                if (response?.Body?.SignProtocolXmlFileResult == null)
                {
                    logger.LogError("Failed to get a valid response from the signing service.");
                    return (int)ExitCodes.Fail;
                }

                FileSystem.Instance.File.WriteAllBytes(xmlFile, response.Body.SignProtocolXmlFileResult);
                return (int)ExitCodes.Ok;
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Exception during {nameof(SignProtocolXmlAsync)}.");
                return (int)ExitCodes.UnexpectedException;
            }
        }
    }
}