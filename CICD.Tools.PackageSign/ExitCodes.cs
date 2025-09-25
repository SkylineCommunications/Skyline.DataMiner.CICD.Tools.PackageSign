namespace Skyline.DataMiner.CICD.Tools.PackageSign
{
    internal enum ExitCodes
    {
        NotImplemented = -2,
        UnexpectedException = -1,
        Ok = 0,
        Fail = 1,
        InvalidPlatform = 2,
        InvalidFileType = 3
    }
}