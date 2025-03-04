namespace Skyline.DataMiner.CICD.Tools.PackageSign
{
    using System;
    using System.IO.Compression;

    using Skyline.DataMiner.CICD.FileSystem;

    internal static class DmappConverter
    {
        public static string ConvertToDmapp(string path, string outputDirectory, string fileName)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(path));
            ArgumentNullException.ThrowIfNull(outputDirectory, nameof(outputDirectory));
            ArgumentNullException.ThrowIfNull(fileName, nameof(fileName));

            string extension = FileSystem.Instance.Path.GetExtension(path);

            if (!extension.Equals(".nupkg"))
            {
                throw new ArgumentException($"The file extension '{extension}' is not supported. Only '.nupkg' files are supported.");
            }
            
            string newPath = FileSystem.Instance.Path.Combine(outputDirectory, $"{fileName}.dmapp");
            FileSystem.Instance.File.Copy(path, newPath, true);
            return newPath;
        }

        public static string ConvertToNupgk(string path, string outputDirectory)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(path));
            ArgumentNullException.ThrowIfNull(outputDirectory, nameof(outputDirectory));

            string extension = FileSystem.Instance.Path.GetExtension(path);

            if (!extension.Equals(".dmapp"))
            {
                throw new ArgumentException($"The file extension '{extension}' is not supported. Only '.dmapp' files are supported.");
            }
            
            string newPath = FileSystem.Instance.Path.Combine(outputDirectory, FileSystem.Instance.Path.GetFileNameWithoutExtension(path) + ".nupkg");
            FileSystem.Instance.File.Copy(path, newPath, true);
            return newPath;
        }

        public static void AddNuspecFileToPackage(string path)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(path));

            string packageName = FileSystem.Instance.Path.GetFileNameWithoutExtension(path);
            using System.IO.FileStream fileStream = FileSystem.Instance.File.Open(path, System.IO.FileMode.Open);
            using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Update);
            ZipArchiveEntry entry = archive.CreateEntry($"{packageName}.nuspec");

            using var writer = new System.IO.StreamWriter(entry.Open());
            string nuspecContent = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>{Environment.NewLine}" +
                                   $"<package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\">{Environment.NewLine}" +
                                   $"    <metadata>{Environment.NewLine}" +
                                   $"        <id>{packageName}_Signed</id>{Environment.NewLine}" +
                                   $"        <version>1.0.0</version>{Environment.NewLine}" +
                                   $"        <description>{packageName}_Signed</description>{Environment.NewLine}" +
                                   $"        <authors>Skyline Communications</authors>{Environment.NewLine}" +
                                   $"    </metadata>{Environment.NewLine}" +
                                   $"</package>";

            writer.Write(nuspecContent);
        }
    }
}