# Skyline.DataMiner.CICD.Tools.PackageSign

## About

This .NET tool allows you to **sign and verify** DataMiner application (`.dmapp`) and protocol (`.dmprotocol`) packages.  
It supports signing using **Azure Key Vault** certificates and verification against either a provided certificate or existing signatures.

## Features

- **Sign** `.dmapp` and `.dmprotocol` packages using an Azure Key Vault certificate.
- **Verify** `.dmapp` and `.dmprotocol` packages to check if they are signed and valid.
- **Protocol XML Signing** via the Protocol Signing Service for `.dmprotocol` packages.
- **Logging & Debugging** options for enhanced visibility into operations.

## Platform Support

> **Important**  
> The signing and verification functionality of this tool is **only supported on Windows OS**.  

## Azure Key Vault Integration

To sign packages, the tool requires access to a certificate stored in **Azure Key Vault**. For `.dmprotocol` packages, additional authentication to the Protocol Signing Service is required.

### Required Environment Variables

Before using the tool, ensure the following environment variables are set:

**For all package types:**
- **AZURE_TENANT_ID** – The tenant ID of the Azure Active Directory.  
- **AZURE_CLIENT_ID** – The client ID of the Azure application with access to the Key Vault.  
- **AZURE_CLIENT_SECRET** – The client secret for authentication.

**Additional variables for `.dmprotocol` packages:**
- **SIGNING_DOMAIN** – (Optional) Domain of the account to connect to the Protocol Signing Service. Not required if the account has no domain.
- **SIGNING_USERNAME** – Username of the account to connect to the Protocol Signing Service.  
- **SIGNING_PASSWORD** – Password of the account to connect to the Protocol Signing Service.

These variables are required to authenticate with Azure Key Vault and retrieve the signing certificate, and for protocol packages, to authenticate with the Protocol Signing Service. Note that `SIGNING_DOMAIN` is optional for accounts without a domain.

## Usage

### Installation

To install the tool globally using .NET CLI, run:

```sh
dotnet tool install -g Skyline.DataMiner.CICD.Tools.PackageSign
```

### Commands

The tool uses a hierarchical command structure with two main commands: `sign` and `verify`, each supporting both `.dmapp` and `.dmprotocol` package types.

#### Sign a `.dmapp` Package

```sh
dataminer-package-signature sign dmapp --package-location <PathToDmapp> \
                                       --output <OutputDirectory>
```

**Options:**
- `--package-location, -pl` → Path to the `.dmapp` file or directory containing multiple packages (Required).
- `--output, -o` → Directory where the signed package will be stored. If not provided, it will overwrite the provided file(s).

> **Note**: Azure Key Vault credentials (`AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_KEY_VAULT_URL`, `AZURE_KEY_VAULT_CERTIFICATE`) should be set as environment variables.

#### Sign a `.dmprotocol` Package

```sh
dataminer-package-signature sign dmprotocol --package-location <PathToDmprotocol> \
                                            --output <OutputDirectory>
```

**Options:**
- `--package-location, -pl` → Path to the `.dmprotocol` file or directory containing multiple packages (Required).
- `--output, -o` → Directory where the signed package will be stored. If not provided, it will overwrite the provided file(s).

> **Note**: Azure Key Vault credentials (`AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_KEY_VAULT_URL`, `AZURE_KEY_VAULT_CERTIFICATE`) and Protocol Signing Service credentials (`SIGNING_DOMAIN`, `SIGNING_USERNAME`, `SIGNING_PASSWORD`) should be set as environment variables.

#### Verify a `.dmapp` Package

```sh
dataminer-package-signature verify dmapp --package-location <PathToDmapp>
```

**Options:**
- `--package-location, -pl` → Path to the `.dmapp` file or directory containing multiple packages (Required).

> **Note**: If you want to verify against a specific certificate, set the Azure Key Vault environment variables (`AZURE_KEY_VAULT_URL`, `AZURE_KEY_VAULT_CERTIFICATE`) or use the corresponding command-line parameters.

#### Verify a `.dmprotocol` Package

```sh
dataminer-package-signature verify dmprotocol --package-location <PathToDmprotocol>
```

**Options:**
- `--package-location, -pl` → Path to the `.dmprotocol` file or directory containing multiple packages (Required).

> **Note**: If you want to verify against a specific certificate, set the Azure Key Vault environment variables (`AZURE_KEY_VAULT_URL`, `AZURE_KEY_VAULT_CERTIFICATE`) or use the corresponding command-line parameters.

If no certificate is provided for verification commands, the tool will verify if the package is signed but will not check against a specific certificate.

## About DataMiner

DataMiner is a transformational platform that provides vendor-independent control and monitoring of devices and services. It addresses key challenges such as security, complexity, multi-cloud environments, and more. 

The foundation of DataMiner is its powerful and versatile data acquisition and control layer. Data sources may reside **on-premises, in the cloud, or in a hybrid setup**.

A unique catalog of **7,000+ connectors** already exists. Additionally, you can leverage DataMiner Development Packages to build your own connectors (also known as **"protocols" or "drivers"**).

> **Note**  
> See also: [About DataMiner](https://aka.dataminer.services/about-dataminer).

## About Skyline Communications

At Skyline Communications, we develop world-class solutions that are deployed by leading companies worldwide. Check out [our proven track record](https://aka.dataminer.services/about-skyline) and see how we empower our customers to elevate their operations.