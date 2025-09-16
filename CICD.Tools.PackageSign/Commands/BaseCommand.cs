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
        }
    }

    internal abstract class BaseCommandHandler : ICommandHandler
    {
        public required IFileSystemInfoIO PackageLocation { get; set; }

        public int Invoke(InvocationContext context)
        {
            return (int)ExitCodes.NotImplemented;
        }

        public abstract Task<int> InvokeAsync(InvocationContext context);
    }
}