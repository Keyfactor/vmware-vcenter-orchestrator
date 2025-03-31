<h1 align="center" style="border-bottom: none">
    VMware vCenter Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/vmware-vcenter-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/vmware-vcenter-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/vmware-vcenter-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/vmware-vcenter-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>

## Overview

The VMware vCenter Universal Orchestrator extension remotely manages certificates used by VMware vCenter. The extension supports the Inventory, Management Add, and Management Remove job types. This enables the capability to create and remove trusted root chains and SSL certificates associated with VMware vCenter.

VMware vCenter uses certificates to secure communications between the different components of the vSphere environment. These certificates ensure data integrity, confidentiality, and authenticity. Managing these certificates is crucial for maintaining the security of the vSphere infrastructure. The VMware vCenter Universal Orchestrator extension automates and simplifies this process by integrating seamlessly with Keyfactor Command.



### vCenter

The certificate store type of vCenter associated with this integration implements the Inventory, Management Add, and Management Remove job types.

The Add and Remove operations have the ability to create and remove trusted root chains and SSL certificates associated with
VMware vCenter. The certificate type is automatically identified by the orchestrator.  It does not manage ESXI host certificates.

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The VMware vCenter Universal Orchestrator extension If you have a support issue, please open a support ticket by either contacting your Keyfactor representative or via the Keyfactor Support Portal at https://support.keyfactor.com. 
 
> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the VMware vCenter Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.



## Create the vCenter Certificate Store Type

To use the VMware vCenter Universal Orchestrator extension, you **must** create the vCenter Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.



* **Create vCenter using kfutil**:

    ```shell
    # VMware vCenter
    kfutil store-types create vCenter
    ```

* **Create vCenter manually in the Command UI**:
    <details><summary>Create vCenter manually in the Command UI</summary>

    Create a store type called `vCenter` with the attributes in the tables below:

    #### Basic Tab
    | Attribute | Value | Description |
    | --------- | ----- | ----- |
    | Name | VMware vCenter | Display name for the store type (may be customized) |
    | Short Name | vCenter | Short display name for the store type |
    | Capability | vCenter | Store type name orchestrator will register with. Check the box to allow entry of value |
    | Supports Add | ✅ Checked | Check the box. Indicates that the Store Type supports Management Add |
    | Supports Remove | ✅ Checked | Check the box. Indicates that the Store Type supports Management Remove |
    | Supports Discovery | 🔲 Unchecked |  Indicates that the Store Type supports Discovery |
    | Supports Reenrollment | 🔲 Unchecked |  Indicates that the Store Type supports Reenrollment |
    | Supports Create | 🔲 Unchecked |  Indicates that the Store Type supports store creation |
    | Needs Server | ✅ Checked | Determines if a target server name is required when creating store |
    | Blueprint Allowed | ✅ Checked | Determines if store type may be included in an Orchestrator blueprint |
    | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
    | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
    | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

    The Basic tab should look like this:

    ![vCenter Basic Tab](docsource/images/vCenter-basic-store-type-dialog.png)

    #### Advanced Tab
    | Attribute | Value | Description |
    | --------- | ----- | ----- |
    | Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias. |
    | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
    | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

    The Advanced tab should look like this:

    ![vCenter Advanced Tab](docsource/images/vCenter-advanced-store-type-dialog.png)

    > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

    #### Custom Fields Tab
    Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

    | Name | Display Name | Description | Type | Default Value/Options | Required |
    | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
    | ServerUsername | Server Username | The vCenter username used to manage the vCenter connection | Secret |  | ✅ Checked |
    | ServerPassword | Server Password | The secret vCenter password used to manage the vCenter connection | Secret |  | ✅ Checked |

    The Custom Fields tab should look like this:

    ![vCenter Custom Fields Tab](docsource/images/vCenter-custom-fields-store-type-dialog.png)



    </details>

## Installation

1. **Download the latest VMware vCenter Universal Orchestrator extension from GitHub.** 

    Navigate to the [VMware vCenter Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/vmware-vcenter-orchestrator/releases/latest). Refer to the compatibility matrix below to determine whether the `net6.0` or `net8.0` asset should be downloaded. Then, click the corresponding asset to download the zip archive.
    | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `vmware-vcenter-orchestrator` .NET version to download |
    | --------- | ----------- | ----------- | ----------- |
    | Older than `11.0.0` | | | `net6.0` |
    | Between `11.0.0` and `11.5.1` (inclusive) | `net6.0` | | `net6.0` | 
    | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `Disable` | `net6.0` | 
    | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` | 
    | `11.6` _and_ newer | `net8.0` | | `net8.0` |

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net6.0`.

2. **Locate the Universal Orchestrator extensions directory.**

    * **Default on Windows** - `C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions`
    * **Default on Linux** - `/opt/keyfactor/orchestrator/extensions`
    
3. **Create a new directory for the VMware vCenter Universal Orchestrator extension inside the extensions directory.**
        
    Create a new directory called `vmware-vcenter-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

4. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `vmware-vcenter-orchestrator` directory.**

5. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).



> The above installation steps can be supplimented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores



* **Manually with the Command UI**

    <details><summary>Create Certificate Stores manually in the UI</summary>

    1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

        Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

    2. **Add a Certificate Store.**

        Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.
        | Attribute | Description |
        | --------- | ----------- |
        | Category | Select "VMware vCenter" or the customized certificate store name from the previous step. |
        | Container | Optional container to associate certificate store with. |
        | Client Machine | The domain name of the vSphere client managing vCenter (url to vCenter host without the 'https://'. |
        | Store Path | A unique identifier for this store.  The actual value is unused by the orchestrator extension |
        | Orchestrator | Select an approved orchestrator capable of managing `vCenter` certificates. Specifically, one with the `vCenter` capability. |
        | ServerUsername | The vCenter username used to manage the vCenter connection |
        | ServerPassword | The secret vCenter password used to manage the vCenter connection |


        

    </details>

* **Using kfutil**
    
    <details><summary>Create Certificate Stores with kfutil</summary>
    
    1. **Generate a CSV template for the vCenter certificate store**

        ```shell
        kfutil stores import generate-template --store-type-name vCenter --outpath vCenter.csv
        ```
    2. **Populate the generated CSV file**

        Open the CSV file, and reference the table below to populate parameters for each **Attribute**.
        | Attribute | Description |
        | --------- | ----------- |
        | Category | Select "VMware vCenter" or the customized certificate store name from the previous step. |
        | Container | Optional container to associate certificate store with. |
        | Client Machine | The domain name of the vSphere client managing vCenter (url to vCenter host without the 'https://'. |
        | Store Path | A unique identifier for this store.  The actual value is unused by the orchestrator extension |
        | Orchestrator | Select an approved orchestrator capable of managing `vCenter` certificates. Specifically, one with the `vCenter` capability. |
        | ServerUsername | The vCenter username used to manage the vCenter connection |
        | ServerPassword | The secret vCenter password used to manage the vCenter connection |


        

    3. **Import the CSV file to create the certificate stores** 

        ```shell
        kfutil stores import csv --store-type-name vCenter --file vCenter.csv
        ```
    </details>

> The content in this section can be supplimented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).


### vCenter Configuration

vCenter management is controlled by the vSphere client. Follow VMware's vCenter Server Configuration [documentation](https://docs.vmware.com/en/VMware-vSphere/7.0/vsphere-esxi-vcenter-server-703-configuration-guide.pdf) to configure a vSphere client and vCenter.

### Installing the extension

1. Stop the Orchestrator service if it is running.
1. Create a folder in your Orchestrator extensions directory called "vCenter"
1. Extract the contents of the release zip file into this folder.
1. Start the Orchestrator service.

### vCenter Certificate Store Parameters

To create a new certificate store in Keyfactor Command, select the _Locations_ drop down, select _Certificate Stores_, and click the _Add_ button.
fill the displayed form with the following values:

| Parameter       | Value                  | Description                                                                         |
|-----------------|------------------------|-------------------------------------------------------------------------------------|
| Category        | 'VMware vCenter'       | The name of the VMware vCenter store type                                           |
| Client Machine  | vSphere Domain Name    | The domain name of the vSphere client managing vCenter (ex: https://myvcenter.pki.local would use `myvcenter.pki.local`                             |
| Store Path      | 'vCenter Certificates' | The _StorePathValue_ of the vCenter instance as set during store type configuration |
| Server Username | Client secret Username | The secret vCenter username used to manage the vCenter connection                   |
| Server Password | Client Secret Password | The secret vCenter password used to manage the vCenter connection                   |

### Managing vCenter Certificates
 
This orchestrator extension allows managing both Trusted root certificates as well as SSL/TLS certificates.  

:warning: _Important note on certificate enrollment_

In order to enroll a new Trusted Root Certificate from the platform, follow the normal steps for enrolling a certificate into the certificate store, but do not include the private key.
- If the private key is omitted, the extension assumes we are replacing the Trusted Root Certificate.
- If the private key is included, the extension assumes we are replacing the TLS certificate used for SSL communication.



## Managing vCenter Certificates

This orchestrator extension allows managing both Trusted root certificates as well as SSL/TLS certificates.

### TLS replacement vs. Adding Trusted Roots

In order to enroll a new **Trusted Root** Certificate from the platform, follow the normal steps for enrolling a certificate into the certificate store, but do not include the private key.
- **If the private key is omitted**: the extension assumes we are replacing the **Trusted Root Certificate**.
- **If the private key is included**: the extension assumes we are replacing the **TLS certificate** used for SSL communication.

:warning: **Important note about Trusted Root Chain Removal**

Trusted root chains can be added and removed from the vCenter certificate store through the orchestrator. Note that the vCenter instance will be put into a bad state if the trusted root of the SSL certificate corresponding to the vSphere server is deleted from the certificate store.


## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).