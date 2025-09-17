namespace Skyline.DataMiner.CICD.Tools.PackageSign.Commands
{
    using Skyline.DataMiner.CICD.Tools.PackageSign.SystemCommandLine;

    internal class BaseCommand : Command
    {
        protected BaseCommand(string name, string? description = null) : base(name, description)
        {
            AddOption(new Option<IFileSystemInfoIO>(
                aliases: ["--package-location", "-pl"],
                description: "Path to the location where the DataMiner package reside. Can be a direct path to the file or a directory containing multiple packages.",
                parseArgument: OptionHelper.ParseFileSystemInfo!)
            {
                IsRequired = true
            }!.ExistingOnly().LegalFilePathsOnly());

            AddOption(new Option<string?>(
                aliases: ["--azure-tenant-id", "-ati"],
                description: "Azure tenant ID for authenticating towards the Azure Key Vault"));

            AddOption(new Option<string?>(
                aliases: ["--azure-client-id", "-aci"],
                description: "Azure client ID for authenticating towards the Azure Key Vault"));

            AddOption(new Option<string?>(
                aliases: ["--azure-client-secret", "-acs"],
                description: "Azure client secret for authenticating towards the Azure Key Vault"));

            AddOption(new Option<Uri?>(
                aliases: ["--azure-key-vault-url", "-kvu"],
                description: "URL to an Azure Key Vault."));

            AddOption(new Option<string?>(
                aliases: ["--azure-key-vault-certificate", "-kvc"],
                description: "Name of the certificate in Azure Key Vault."));
        }
    }

    internal abstract class BaseCommandHandler : ICommandHandler
    {
        public required IFileSystemInfoIO PackageLocation { get; set; }

        public string? AzureTenantId { get; set; }

        public string? AzureClientId { get; set; }

        public string? AzureClientSecret { get; set; }

        public Uri? AzureKeyVaultUri { get; set; }

        public string? AzureKeyVaultCertificate { get; set; }

        public int Invoke(InvocationContext context)
        {
            return (int)ExitCodes.NotImplemented;
        }

        public abstract Task<int> InvokeAsync(InvocationContext context);
    }
}