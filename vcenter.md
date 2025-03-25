## Overview

The certificate store type of vCenter associated with this integration implements the Inventory, Management Add, and Management Remove job types.

The Add and Remove operations have the ability to create and remove trusted root chains and SSL certificates associated with
VMware vCenter. The certificate type is automatically identified by the orchestrator.  It does not manage ESXI host certificates.

## vCenter Configuration

vCenter management is controlled by the vSphere client. Follow VMware's vCenter Server Configuration [documentation](https://docs.vmware.com/en/VMware-vSphere/7.0/vsphere-esxi-vcenter-server-703-configuration-guide.pdf) to configure a vSphere client and vCenter.

## Installing the extension

1. Stop the Orchestrator service if it is running.
1. Create a folder in your Orchestrator extensions directory called "vCenter"
1. Extract the contents of the release zip file into this folder.
1. Start the Orchestrator service.

## vCenter Certificate Store Parameters

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
