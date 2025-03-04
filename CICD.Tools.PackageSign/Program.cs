namespace Skyline.DataMiner.CICD.Tools.PackageSign
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    using Serilog;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.PackageSign.NuGetSigningAndVerifying;

    using ILogger = Microsoft.Extensions.Logging.ILogger;

    /// <summary>
    /// TBD.
    /// </summary>
    public static class Program
    {
        /*
         * Design guidelines for command line tools: https://learn.microsoft.com/en-us/dotnet/standard/commandline/syntax#design-guidance
         */

        /// <summary>
        /// Code that will be called when running the tool.
        /// </summary>
        /// <param name="args">Extra arguments.</param>
        /// <returns>0 if successful.</returns>
        public static async Task<int> Main(string[] args)
        {
            var isDebug = new Option<bool>(
                name: "--debug",
                description: "Indicates the tool should write out debug logging.")
            {
                IsRequired = false
            };

            isDebug.SetDefaultValue(false);

            var dmappLocation = new Argument<System.IO.FileInfo>(
                name: "package-location",
                description: "Path to the location where the DataMiner application packages reside. Can be a direct path to the file or a directory containing files.");
            dmappLocation.AddValidator(result =>
            {
                System.IO.FileInfo value = result.GetValueOrDefault<System.IO.FileInfo>();
                if (value == null)
                {
                    result.ErrorMessage = "The package location is required.";
                    return;
                }

                if (value.Extension != ".dmapp")
                {
                    result.ErrorMessage = "The package location must be a .dmapp file.";
                    return;
                }

                if (!value.Exists)
                {
                    result.ErrorMessage = "The package does not exist.";
                    return;
                }

                // Path is correct
            });

            Option<Uri> urlOption = new(["--azure-key-vault-url", "-kvu"], "URL to an Azure Key Vault.")
            {
                IsRequired = true,
            };
            Option<string> certificateOption = new(["--azure-key-vault-certificate", "-kvc"], "Name of the certificate in Azure Key Vault.")
            {
                IsRequired = true,
            };
            Option<System.IO.DirectoryInfo> outputOption = new(["--output", "-o"], "Output directory for the signed packages.")
            {
                IsRequired = true,
            };
            
            var rootCommand = new RootCommand("This .NET tool allows you to sign and verify DataMiner application (.dmapp) packages.");
            rootCommand.AddGlobalOption(isDebug);

            var signCommand = new Command("sign", "Signs a DataMiner application (.dmapp) package.")
            {
                dmappLocation,
                urlOption,
                certificateOption,
                outputOption
            };

            var verifyCommand = new Command("verify", "Verifies a DataMiner application (.dmapp) package.")
            {
                dmappLocation,
                urlOption,
                certificateOption,
            };


            signCommand.SetHandler(SignAsync, isDebug, dmappLocation, certificateOption, urlOption, outputOption);
            verifyCommand.SetHandler(VerifyAsync, isDebug, dmappLocation, certificateOption, urlOption);

            rootCommand.AddCommand(signCommand);

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task<int> VerifyAsync(bool isDebug, System.IO.FileInfo dmappLocation, string certificateId, Uri url)
        {
            try
            {
                ILogger logger = GetLogger(isDebug);
                IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

                return await VerifyInternalAsync(configuration, dmappLocation.FullName, certificateId, url, logger);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception on Logger Creation: {e}");
                return 1;
            }
        }

        internal static async Task<int> VerifyInternalAsync(IConfiguration configuration, string dmappLocation, string certificateId, Uri url, ILogger logger)
        {
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();

            try
            {
                SignatureInfo signatureInfo = await SignatureInfo.GetAsync(configuration, certificateId, url, logger);

                string nupgkFilePath = DmappConverter.ConvertToNupgk(dmappLocation, temporaryDirectory);
                var verifier = new NuGetPackageSignerAndVerifier(logger);
                if (await verifier.VerifyAsync(nupgkFilePath, signatureInfo))
                {
                    logger.LogDebug($"Successfully verified the signed dmapp package: '{dmappLocation}'");
                    return 0;
                }
                else
                {
                    logger.LogError($"Failed to verify the package: {dmappLocation}");
                    return 1;
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Exception during Process Run: {e}");
                return 1;
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
            }
        }

        private static async Task<int> SignAsync(bool isDebug, System.IO.FileInfo dmappLocation, string certificateId, Uri url, System.IO.DirectoryInfo outputPath)
        {
            try
            {
                ILogger logger = GetLogger(isDebug);
                IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

                return await SignInternalAsync(configuration, dmappLocation.FullName, certificateId, url, outputPath.FullName, logger);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception on Logger Creation: {e}");
                return 1;
            }
        }

        internal static async Task<int> SignInternalAsync(IConfiguration configuration, string dmappLocation, string certificateId, Uri url, string outputPath, ILogger logger)
        {
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();

            try
            {
                string packageName = FileSystem.Instance.Path.GetFileNameWithoutExtension(dmappLocation);
                if (await VerifyInternalAsync(configuration, dmappLocation, certificateId, url, logger) == 0)
                {
                    // Already signed, move to output directory
                    FileSystem.Instance.File.Copy(dmappLocation, FileSystem.Instance.Path.Combine(outputPath, $"{packageName}.dmapp"));
                    return 0;
                }

                // TODO: See if it's worth it to add a check if the package already has a nuspec file (previous signing that went wrong or trying to resign with different certificate)

                SignatureInfo signatureInfo = await SignatureInfo.GetAsync(configuration, certificateId, url, logger);
                
                string nupgkFilePath = DmappConverter.ConvertToNupgk(dmappLocation, temporaryDirectory);
                DmappConverter.AddNuspecFileToPackage(nupgkFilePath);

                var signer = new NuGetPackageSignerAndVerifier(logger);
                string signedNupkgFilePath = FileSystem.Instance.Path.Combine(temporaryDirectory, packageName + "_Signed.nupkg");
                if (await signer.SignAsync(nupgkFilePath, signedNupkgFilePath, signatureInfo, true))
                {
                    // Create directory in case it doesn't exist yet
                    FileSystem.Instance.Directory.CreateDirectory(outputPath);
                    string dmappFilePath = DmappConverter.ConvertToDmapp(signedNupkgFilePath, outputPath, packageName);
                    logger.LogDebug($"Created signed dmapp package at '{dmappFilePath}'");
                    return 0;
                }
                else
                {
                    logger.LogError($"Failed to sign the package: {dmappLocation}");
                    return 1;
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Exception during Process Run: {e}");
                return 1;
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
            }
        }

        private static ILogger GetLogger(bool isDebug)
        {
            var logConfig = new LoggerConfiguration().WriteTo.Console();
            logConfig.MinimumLevel.Is(isDebug ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information);
            var seriLog = logConfig.CreateLogger();

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(seriLog));
            var logger = loggerFactory.CreateLogger("Skyline.DataMiner.Utils.Tools.PackageSignAndVerify");
            return logger;
        }
    }
}