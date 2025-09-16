namespace Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Sign
{
    public class SignCommand : Command
    {
        public SignCommand() : base(name: "sign", description: "Signing of packages")
        {
            AddCommand(new SignDmappCommand());
            //AddCommand(new SignDmProtocolCommand());
        }
    }
}