# Skyline.DataMiner.CICD.Tools.PackageSign - Tests

## About

This project contains integration and unit tests for the **Skyline.DataMiner.CICD.Tools.PackageSign** tool.  
The tests validate the signing and verification functionality of `.dmapp` packages using **Azure Key Vault** certificates. 

> **Note**  
> Currently, the tests focus on `.dmapp` packages. The tool also supports `.dmprotocol` packages, but these are not covered by the current test suite.

## Prerequisites

To run the tests, you must configure authentication details for **Azure Key Vault**. These details can be provided through **User Secrets** (recommended) or **Environment Variables**.

### Required Configuration

The following values must be set:

- **`azure-key-vault-certificate`** → Name of the certificate in Azure Key Vault.  
- **`azure-key-vault-url`** → URL of the Azure Key Vault.  
- **`AZURE_TENANT_ID`** → The tenant ID of the Azure Active Directory.  
- **`AZURE_CLIENT_ID`** → The client ID of the Azure application with access to the Key Vault.  
- **`AZURE_CLIENT_SECRET`** → The client secret for authentication.  

## Configuration Options

### Option 1: Using User Secrets (Recommended)

User Secrets allow you to store sensitive information securely without modifying system-wide settings.  

#### Setting Up User Secrets in Visual Studio

1. **Right-click** the test project in **Solution Explorer**.  
2. Select **Manage User Secrets** from the context menu.  
3. This will open a `secrets.json` file. Update it with your Azure Key Vault credentials:

   ```json
   {
     "azure-key-vault-certificate": "YourCertificateName",
     "azure-key-vault-url": "https://your-keyvault-url.vault.azure.net/",
     "AZURE_TENANT_ID": "YourTenantID",
     "AZURE_CLIENT_ID": "YourClientID",
     "AZURE_CLIENT_SECRET": "YourClientSecret"
   }
   ```

4. Save the file and close it.

### Option 2: Using Environment Variables

Alternatively, you can set these values as environment variables in your operating system.

#### On Windows  
1. Open the **Start Menu** and search for **Environment Variables**.  
2. Click **Edit the system environment variables**.  
3. In the **System Properties** window, click **Environment Variables**.  
4. Under **User variables**, click **New** and add the following entries:

   - **Variable name:** `azure-key-vault-certificate`  
     **Variable value:** `YourCertificateName`  
   - **Variable name:** `azure-key-vault-url`  
     **Variable value:** `https://your-keyvault-url.vault.azure.net/`  
   - **Variable name:** `AZURE_TENANT_ID`  
     **Variable value:** `YourTenantID`  
   - **Variable name:** `AZURE_CLIENT_ID`  
     **Variable value:** `YourClientID`  
   - **Variable name:** `AZURE_CLIENT_SECRET`  
     **Variable value:** `YourClientSecret`  

5. Click **OK** to save and apply changes.

## Running the Tests

Once the required variables are set, you can run the tests using:

```sh
dotnet test
```

Make sure all authentication details are correctly configured before executing the tests.  