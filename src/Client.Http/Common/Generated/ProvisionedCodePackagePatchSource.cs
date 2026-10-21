// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A replacement code package from an already provisioned application type.
    /// Service Fabric resolves its contents and checksum from the provisioned package metadata and publishes a retained
    /// copy under the target application type using the normal validation policy.
    /// Removing the source registration after publication does not remove that retained copy. Automatic remote cleanup
    /// occurs when the last registered version of the target application type is unregistered.
    /// </summary>
    public partial class ProvisionedCodePackagePatchSource : CodePackagePatchSource
    {
        /// <summary>
        /// Initializes a new instance of the ProvisionedCodePackagePatchSource class.
        /// </summary>
        /// <param name="applicationTypeName">The application type name as defined in the application manifest.</param>
        /// <param name="applicationTypeVersion">The registered application-type version containing the replacement source, not
        /// the original CodePackageVersion being patched.</param>
        /// <param name="serviceManifestName">The name of the service manifest.</param>
        /// <param name="sourceCodePackageName">The name of the code package defined in the service manifest.</param>
        public ProvisionedCodePackagePatchSource(
            string applicationTypeName,
            string applicationTypeVersion,
            string serviceManifestName,
            string sourceCodePackageName)
            : base(
                Common.CodePackagePatchSourceKind.ProvisionedCodePackage)
        {
            applicationTypeName.ThrowIfNull(nameof(applicationTypeName));
            applicationTypeVersion.ThrowIfNull(nameof(applicationTypeVersion));
            serviceManifestName.ThrowIfNull(nameof(serviceManifestName));
            sourceCodePackageName.ThrowIfNull(nameof(sourceCodePackageName));
            this.ApplicationTypeName = applicationTypeName;
            this.ApplicationTypeVersion = applicationTypeVersion;
            this.ServiceManifestName = serviceManifestName;
            this.SourceCodePackageName = sourceCodePackageName;
        }

        /// <summary>
        /// Gets the application type name as defined in the application manifest.
        /// </summary>
        public string ApplicationTypeName { get; }

        /// <summary>
        /// Gets the registered application-type version containing the replacement source, not the original CodePackageVersion
        /// being patched.
        /// </summary>
        public string ApplicationTypeVersion { get; }

        /// <summary>
        /// Gets the name of the service manifest.
        /// </summary>
        public string ServiceManifestName { get; }

        /// <summary>
        /// Gets the name of the code package defined in the service manifest.
        /// </summary>
        public string SourceCodePackageName { get; }
    }
}
