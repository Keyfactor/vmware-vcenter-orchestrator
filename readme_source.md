## Overview

The VMware vCenter Orchestrator extension remotely manages certificates used by VMware vCenter. The extension implements the Inventory, Management Add, and Management Remove job types.

The Add and Remove operations have the ability to create and remove trusted root chains and SSL certificates associated with
VMware vCenter. The certificate type is automatically identified by the orchestrator.  It does not manage ESXI host certificates.

## vCenter Configuration

vCenter management is controlled by the vSphere client. Follow VMware's vCenter Server Configuration [documentation](https://docs.vmware.com/en/VMware-vSphere/7.0/vsphere-esxi-vcenter-server-703-configuration-guide.pdf) to configure a vSphere client and vCenter.

## Installing the extension

1. Stop the Orchestrator service if it is running.
1. Create a folder in your Orchestrator extensions directory called "vCenter"
1. Extract the contents of the release zip file into this folder.
1. Start the Orchestrator service.

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
      "Remove": false
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
| Client Machine  | vSphere Domain Name    | The domain name of the vSphere client managing vCenter (ex: https://myvcenter.pki.local would use `myvcenter.pki.local`                             |
| Store Path      | 'vCenter Certificates' | The _StorePathValue_ of the vCenter instance as set during store type configuration |
| Server Username | Client secret Username | The secret vCenter username used to manage the vCenter connection                   |
| Server Password | Client Secret Password | The secret vCenter password used to manage the vCenter connection                   |

## Managing vCenter Certificates

This orchestrator extension allows managing both Trusted root certificates as well as SSL/TLS certificates.  

:warning: _Important note on certificate enrollment_

In order to enroll a new Trusted Root Certificate from the platform, follow the normal steps for enrolling a certificate into the certificate store, but do not include the private key.
- If the private key is omitted, the extension assumes we are replacing the Trusted Root Certificate.
- If the private key is included, the extension assumes we are replacing the TLS certificate used for SSL communication.
