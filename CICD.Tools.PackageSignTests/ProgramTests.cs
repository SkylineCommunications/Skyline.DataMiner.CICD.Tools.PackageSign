namespace CICD.Tools.PackageSignTests
{
    using FluentAssertions;

    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Nito.AsyncEx.Synchronous;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.FileSystem.DirectoryInfoWrapper;
    using Skyline.DataMiner.CICD.FileSystem.FileInfoWrapper;
    using Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Sign;
    using Skyline.DataMiner.CICD.Tools.PackageSign.Commands.Verify;

    [TestClass, TestCategory("IntegrationTest")]
    public class ProgramTests
    {
        private static IConfiguration configuration;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            configuration = new ConfigurationBuilder()
                            .AddUserSecrets<ProgramTests>() // Use any type from your project
                            .AddEnvironmentVariables()
                            .Build();
        }

        [TestMethod]
        public void VerifyAsyncTest_NonSignedPackage_ShouldReturn1()
        {
            if (configuration == null)
            {
                Assert.Fail("Failed to load configuration.");
                return;
            }

            // Arrange
            var dmappLocation = new FileInfo(FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0.dmapp"));
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => VerifyDmappCommandHandler.VerifyInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();

            // Assert
            int returnValue = result.Should().NotThrow().Subject;
            returnValue.Should().Be(1);
        }

        [TestMethod]
        public void VerifyAsyncTest_SignedPackageButModified_ShouldReturn1()
        {
            if (configuration == null)
            {
                Assert.Fail("Failed to load configuration.");
                return;
            }

            // Arrange
            var dmappLocation = new FileInfo(FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0_SignedButModified.dmapp"));
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => VerifyDmappCommandHandler.VerifyInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();

            // Assert
            int returnValue = result.Should().NotThrow().Subject;
            returnValue.Should().Be(1);
        }

        [TestMethod]
        public void VerifyAsyncTest_SignedPackage_ShouldReturn0()
        {
            if (configuration == null)
            {
                Assert.Fail("Failed to load configuration.");
                return;
            }

            // Arrange
            var dmappLocation = new FileInfo(FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0_Signed.dmapp"));
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => VerifyDmappCommandHandler.VerifyInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();

            // Assert
            int returnValue = result.Should().NotThrow().Subject;
            logger.ErrorLogging.Should().BeEmpty();
            returnValue.Should().Be(0);
        }

        [TestMethod]
        public void SignAsyncTest_NonSignedPackage_ShouldBeAbleToVerify()
        {
            if (configuration == null)
            {
                Assert.Fail("Failed to load configuration.");
                return;
            }

            // Arrange
            var temporaryDirectory = new DirectoryInfo(FileSystem.Instance.Directory.CreateTemporaryDirectory());
            var dmappLocation = new FileInfo(FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0.dmapp"));
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            try
            {
                // Act
                Func<int> result = () => SignDmappCommandHandler.SignInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), temporaryDirectory, logger).WaitAndUnwrapException();

                // Assert
                int returnValue = result.Should().NotThrow().Subject;
                returnValue.Should().Be(0);
                var signedPackageLocation = new FileInfo(FileSystem.Instance.Path.Combine(temporaryDirectory.FullName, "Project7.1.0.0.dmapp"));
                signedPackageLocation.Exists.Should().BeTrue();
                
                int verifyResult = VerifyDmappCommandHandler.VerifyInternalAsync(configuration, signedPackageLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();
                verifyResult.Should().Be(0);
            }
            finally
            {
                temporaryDirectory.Delete(true);
            }
        }

        [TestMethod]
        public void SignAsyncTest_AlreadySignedPackage_ShouldBeAbleToVerify()
        {
            if (configuration == null)
            {
                Assert.Fail("Failed to load configuration.");
                return;
            }

            // Arrange
            var temporaryDirectory = new DirectoryInfo(FileSystem.Instance.Directory.CreateTemporaryDirectory());
            var dmappLocation = new FileInfo(FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0_Signed.dmapp"));
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            try
            {
                // Act
                Func<int> result = () => SignDmappCommandHandler.SignInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), temporaryDirectory, logger).WaitAndUnwrapException();

                // Assert
                int returnValue = result.Should().NotThrow().Subject;
                returnValue.Should().Be(0);
                var signedPackageLocation = new FileInfo(FileSystem.Instance.Path.Combine(temporaryDirectory.FullName, "Project7.1.0.0_Signed.dmapp"));
                signedPackageLocation.Exists.Should().BeTrue();

                int verifyResult = VerifyDmappCommandHandler.VerifyInternalAsync(configuration, signedPackageLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();
                verifyResult.Should().Be(0);
            }
            finally
            {
                temporaryDirectory.Delete(true);
            }
        }
    }
}