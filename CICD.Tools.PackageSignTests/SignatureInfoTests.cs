namespace CICD.Tools.PackageSignTests
{
    using FluentAssertions;

    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Nito.AsyncEx.Synchronous;

    using Skyline.DataMiner.CICD.Tools.PackageSign;

    [TestClass, TestCategory("IntegrationTest")]
    public class SignatureInfoTests
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
        public void GetSignatureInfoTest()
        {
            if (configuration == null)
            {
                Assert.Fail("Failed to load configuration.");
                return;
            }

            // Arrange
            string certificateId = configuration["azure-key-vault-certificate"];
            string url = configuration["azure-key-vault-url"];

            // Act
            Func<SignatureInfo> func = () => SignatureInfo.GetAsync(configuration, certificateId, new Uri(url), null).WaitAndUnwrapException();

            // Assert
            func.Should().NotThrow();

            SignatureInfo signatureInfo = func.Invoke();

            signatureInfo.Should().NotBeNull();
            signatureInfo.Certificate.Should().NotBeNull();
            signatureInfo.Rsa.Should().NotBeNull();
        }
    }
}