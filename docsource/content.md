## Overview

The VMware vCenter Universal Orchestrator extension remotely manages certificates used by VMware vCenter. The extension supports the Inventory, Management Add, and Management Remove job types. This enables the capability to create and remove trusted root chains and SSL certificates associated with VMware vCenter.

VMware vCenter uses certificates to secure communications between the different components of the vSphere environment. These certificates ensure data integrity, confidentiality, and authenticity. Managing these certificates is crucial for maintaining the security of the vSphere infrastructure. The VMware vCenter Universal Orchestrator extension automates and simplifies this process by integrating seamlessly with Keyfactor Command.

## Managing vCenter Certificates

This orchestrator extension allows managing both Trusted root certificates as well as SSL/TLS certificates.

### TLS replacement vs. Adding Trusted Roots

In order to enroll a new **Trusted Root** Certificate from the platform, follow the normal steps for enrolling a certificate into the certificate store, but do not include the private key.
- **If the private key is omitted**: the extension assumes we are replacing the **Trusted Root Certificate**.
- **If the private key is included**: the extension assumes we are replacing the **TLS certificate** used for SSL communication.

:warning: **Important note about Trusted Root Chain Removal**

Trusted root chains can be added and removed from the vCenter certificate store through the orchestrator. Note that the vCenter instance will be put into a bad state if the trusted root of the SSL certificate corresponding to the vSphere server is deleted from the certificate store.
