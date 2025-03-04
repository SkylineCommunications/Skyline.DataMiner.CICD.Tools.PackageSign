namespace CICD.Tools.PackageSignTests
{
    using FluentAssertions;

    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Nito.AsyncEx.Synchronous;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.PackageSign;

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
            string dmappLocation = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0.dmapp");
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => Program.VerifyInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();

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
            string dmappLocation = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0_SignedButModified.dmapp");
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => Program.VerifyInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();

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
            string dmappLocation = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0_Signed.dmapp");
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            // Act
            Func<int> result = () => Program.VerifyInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();

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
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            string dmappLocation = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0.dmapp");
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            try
            {
                // Act
                Func<int> result = () => Program.SignInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), temporaryDirectory, logger).WaitAndUnwrapException();

                // Assert
                int returnValue = result.Should().NotThrow().Subject;
                if (logger.ErrorLogging.Count > 0)
                {
                    Assert.Fail($"Logging: {String.Join(Environment.NewLine, logger.Logging)}");
                }

                returnValue.Should().Be(0);
                string signedPackageLocation = FileSystem.Instance.Path.Combine(temporaryDirectory, "Project7.1.0.0.dmapp");
                FileSystem.Instance.File.Exists(signedPackageLocation).Should().BeTrue();
                
                int verifyResult = Program.VerifyInternalAsync(configuration, signedPackageLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();
                verifyResult.Should().Be(0);
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
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
            string temporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            string dmappLocation = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Project7.1.0.0_Signed.dmapp");
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];
            var logger = TestHelper.GetTestLogger();

            try
            {
                // Act
                Func<int> result = () => Program.SignInternalAsync(configuration, dmappLocation, certificateId, new Uri(url), temporaryDirectory, logger).WaitAndUnwrapException();

                // Assert
                int returnValue = result.Should().NotThrow().Subject;
                returnValue.Should().Be(0);
                string signedPackageLocation = FileSystem.Instance.Path.Combine(temporaryDirectory, "Project7.1.0.0_Signed.dmapp");
                FileSystem.Instance.File.Exists(signedPackageLocation).Should().BeTrue();

                int verifyResult = Program.VerifyInternalAsync(configuration, signedPackageLocation, certificateId, new Uri(url), logger).WaitAndUnwrapException();
                verifyResult.Should().Be(0);
            }
            finally
            {
                FileSystem.Instance.Directory.Delete(temporaryDirectory, true);
            }
        }
    }
}