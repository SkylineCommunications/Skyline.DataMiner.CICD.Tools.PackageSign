namespace Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Verify
{
    internal class VerifyCommand : Command
    {
        public VerifyCommand() : base(name: "verify", description: "Verifying of packages")
        {
            AddCommand(new VerifyDmappCommand());
            AddCommand(new VerifyDmprotocolCommand());
        }
    }
}