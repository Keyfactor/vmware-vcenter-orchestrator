# VMware vCenter Orchestrator

The VMware vCenter Orchestrator extension acts as a proxy between Keyfactor and VMware vCenter that allows Keyfactor to manage vCenter certificates.

#### Integration status: Prototype - Demonstration quality. Not for use in customer environments.


## About the Keyfactor Universal Orchestrator Extension

This repository contains a Universal Orchestrator Extension which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Extensions, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Extension see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Orchestrator Extension plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.


## Support for VMware vCenter Orchestrator

VMware vCenter Orchestrator is open source and there is **no SLA** for this tool/library/client. Keyfactor will address issues as resources become available. Keyfactor customers may request escalation by opening up a support ticket through their Keyfactor representative.

###### To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.


---




## Keyfactor Version Supported

The minimum version of the Keyfactor Universal Orchestrator Framework needed to run this version of the extension is 10.1

## Platform Specific Notes

The Keyfactor Universal Orchestrator may be installed on either Windows or Linux based platforms. The certificate operations supported by a capability may vary based what platform the capability is installed on. The table below indicates what capabilities are supported based on which platform the encompassing Universal Orchestrator is running.
| Operation | Win | Linux |
|-----|-----|------|
|Supports Management Add|&check; |&check; |
|Supports Management Remove|&check; |&check; |
|Supports Create Store|  |  |
|Supports Discovery|  |  |
|Supports Renrollment|  |  |
|Supports Inventory|&check; |&check; |





---


## Overview

The VMware vCenter Orchestrator extension remotely manages certificates used by VMware vCenter. The extension implements the Inventory, Management Add, and Management Remove job types.

The Add and Remove operations have the ability to create and remove trusted root chains and SSL certificates associated with
VMware vCenter. The certificate type is automatically identified by the orchestrator.

## vCenter Configuration

vCenter management is controlled by the vSphere client. Follow VMware's vCenter Server Configuration [documentation](https://docs.vmware.com/en/VMware-vSphere/7.0/vsphere-esxi-vcenter-server-703-configuration-guide.pdf) to configure a vSphere client and vCenter.

## Keyfactor Configuration

Follow the Keyfactor Orchestrator configuration guide to install the VMware vCenter Orchestrator extension.

This guide uses the `kfutil` Keyfactor command line tool that offers convenient and powerful
command line access to the Keyfactor platform. Before proceeding, ensure that `kfutil` is installed and configured
by following the instructions here: [https://github.com/Keyfactor/kfutil](https://github.com/Keyfactor/kfutil)

Configuration is done in two steps:
1. Create a new Keyfactor Certificate Store Type
2. Create a new Keyfactor Certificate Store

### Keyfactor Certificate Store Type Configuration

Keyfactor Certificate Store Types are used to define and configure the platforms that store and use certificates that will be managed
by Keyfactor Orchestrators. To create the VMware vCenter Certificate Store Type, run the following command with `kfutil`:
   ```bash
   cat << EOF > ./VmwareVcenter.json
  {
    "Name": "VMware vCenter",
    "ShortName": "vCenter",
    "Capability": "vCenter",
    "StoreType": 107,
    "ImportType": 107,
    "LocalStore": false,
    "SupportedOperations": {
      "Add": true,
      "Create": false,
      "Discovery": false,
      "Enrollment": false,
      "Remove": true
    },
    "Properties": [
      {
        "StoreTypeId": 107,
        "Name": "ServerUsername",
        "DisplayName": "Server Username",
        "Type": "Secret",
        "DependsOn": null,
        "DefaultValue": null,
        "Required": false
      },
      {
        "StoreTypeId": 107,
        "Name": "ServerPassword",
        "DisplayName": "Server Password",
        "Type": "Secret",
        "DependsOn": null,
        "DefaultValue": null,
        "Required": false
      },
      {
        "StoreTypeId": 107,
        "Name": "ServerUseSsl",
        "DisplayName": "Use SSL",
        "Type": "Bool",
        "DependsOn": null,
        "DefaultValue": "true",
        "Required": true
      }
    ],
    "EntryParameters": [],
    "PasswordOptions": {
      "EntrySupported": false,
      "StoreRequired": false,
      "Style": "Default"
    },
    "StorePathValue": "vCenter Certificates",
    "PrivateKeyAllowed": "Optional",
    "JobProperties": [],
    "ServerRequired": true,
    "PowerShell": false,
    "BlueprintAllowed": false,
    "CustomAliasAllowed": "Forbidden",
    "ServerRegistration": 9,
    "InventoryEndpoint": "/AnyInventory/Update",
    "InventoryJobType": "1a063467-c439-4733-a380-c88be52597de",
    "ManagementJobType": "40ec0a7e-d867-466e-8fd7-5d43605b9b10"
  }
   EOF
   kfutil store-types create --from-file VmwareVcenter.json
   ```
### Keyfactor Store Configuration

To create a new certificate store in Keyfactor Command, select the _Locations_ drop down, select _Certificate Stores_, and click the _Add_ button.
fill the displayed form with the following values:

| Parameter       | Value                  | Description                                                                         |
|-----------------|------------------------|-------------------------------------------------------------------------------------|
| Category        | 'VMware vCenter'       | The name of the VMware vCenter store type                                           |
| Client Machine  | vSphere Domain Name    | The domain name of the vSphere client managing vCenter                              |
| Store Path      | 'vCenter Certificates' | The _StorePathValue_ of the vCenter instance as set during store type configuration |
| Server Username | Client secret Username | The secret vCenter username used to manage the vCenter connection                   |
| Server Password | Client Secret Password | The secret vCenter password used to manage the vCenter connection                   |

### Important note about Trusted Root Chain Removal

Trusted root chains can be added and removed from the vCenter certificate store through the orchestrator. Note that the vCenter instance will be put into a bad state if the trusted root of the SSL certificate corresponding to the vSphere server is deleted from the certificate store.

