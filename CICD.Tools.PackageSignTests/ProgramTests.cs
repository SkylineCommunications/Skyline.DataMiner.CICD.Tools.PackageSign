namespace CICD.Tools.PackageSignTests
{
    using FluentAssertions;

    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Nito.AsyncEx.Synchronous;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.FileSystem.DirectoryInfoWrapper;
    using Skyline.DataMiner.CICD.FileSystem.FileInfoWrapper;
    using Skyline.DataMiner.CICD.Tools.PackageSign;
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
        [DataRow("Project7.1.0.0.dmapp")]
        [DataRow("Project7.1.0.0.dmtest")]
        public void VerifyAsyncTest_NonSignedPackage_ShouldReturn1(string fileName)
        {
            if (configuration == null)
            {
                Assert.Fail("Failed to load configuration.");
                return;
            }

            // Arrange
            var dmappLocation = new FileInfo(FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), fileName));
            SigningZipVariables variables = new(configuration);
            variables.SetAzureCredentials();
            variables.SetAzureKeyVaultVariables();
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => VerifyDmappCommandHandler.VerifyInternalAsync(variables, dmappLocation, logger).WaitAndUnwrapException();

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
            SigningZipVariables variables = new(configuration);
            variables.SetAzureCredentials();
            variables.SetAzureKeyVaultVariables();
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => VerifyDmappCommandHandler.VerifyInternalAsync(variables, dmappLocation, logger).WaitAndUnwrapException();

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
            SigningZipVariables variables = new(configuration);
            variables.SetAzureCredentials();
            variables.SetAzureKeyVaultVariables();
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => VerifyDmappCommandHandler.VerifyInternalAsync(variables, dmappLocation, logger).WaitAndUnwrapException();

            // Assert
            int returnValue = result.Should().NotThrow().Subject;
            logger.ErrorLogging.Should().BeEmpty();
            returnValue.Should().Be(0);
        }

        [TestMethod]
        [DataRow("Project7.1.0.0.dmapp")]
        [DataRow("Project7.1.0.0.dmtest")]
        public void SignAsyncTest_NonSignedPackage_ShouldBeAbleToVerify(string fileName)
        {
            if (configuration == null)
            {
                Assert.Fail("Failed to load configuration.");
                return;
            }

            // Arrange
            var temporaryDirectory = new DirectoryInfo(FileSystem.Instance.Directory.CreateTemporaryDirectory());
            var dmappLocation = new FileInfo(FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), fileName));
            SigningZipVariables variables = new(configuration);
            variables.SetAzureCredentials();
            variables.SetAzureKeyVaultVariables();
            var logger = TestHelper.GetTestLogger();

            try
            {
                // Act
                Func<int> result = () => SignDmappCommandHandler.SignInternalAsync(variables, dmappLocation, temporaryDirectory, logger).WaitAndUnwrapException();

                // Assert
                int returnValue = result.Should().NotThrow().Subject;
                returnValue.Should().Be(0);
                var signedPackageLocation = new FileInfo(FileSystem.Instance.Path.Combine(temporaryDirectory.FullName, fileName));
                signedPackageLocation.Exists.Should().BeTrue();
                
                int verifyResult = VerifyDmappCommandHandler.VerifyInternalAsync(variables, signedPackageLocation, logger).WaitAndUnwrapException();
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
            SigningZipVariables variables = new(configuration);
            variables.SetAzureCredentials();
            variables.SetAzureKeyVaultVariables();
            var logger = TestHelper.GetTestLogger();

            try
            {
                // Act
                Func<int> result = () => SignDmappCommandHandler.SignInternalAsync(variables, dmappLocation, temporaryDirectory, logger).WaitAndUnwrapException();

                // Assert
                int returnValue = result.Should().NotThrow().Subject;
                returnValue.Should().Be(0);
                var signedPackageLocation = new FileInfo(FileSystem.Instance.Path.Combine(temporaryDirectory.FullName, "Project7.1.0.0_Signed.dmapp"));
                signedPackageLocation.Exists.Should().BeTrue();

                int verifyResult = VerifyDmappCommandHandler.VerifyInternalAsync(variables, signedPackageLocation, logger).WaitAndUnwrapException();
                verifyResult.Should().Be(0);
            }
            finally
            {
                temporaryDirectory.Delete(true);
            }
        }
    }
}