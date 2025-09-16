namespace Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Verify
{
    public class VerifyCommand : Command
    {
        public VerifyCommand() : base(name: "verify", description: "Verifying of packages")
        {
            AddCommand(new VerifyDmappCommand());
        }
    }
}