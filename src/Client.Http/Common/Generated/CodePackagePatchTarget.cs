// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The immutable application instance, declared code package version, and scope resolved by Service Fabric.
    /// Use the values returned by GetCodePackagePatchInfoList. The application instance ID is not the encoded
    /// application-name ID used in REST resource paths.
    /// Omitting PartitionId identifies the package-wide mapping. Including PartitionId identifies only that partition's
    /// mapping.
    /// </summary>
    public partial class CodePackagePatchTarget
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchTarget class.
        /// </summary>
        /// <param name="applicationInstanceId">The opaque identity of one application creation. Deleting and recreating the
        /// same application name produces a different identity.</param>
        /// <param name="serviceManifestName">The name of the service manifest.</param>
        /// <param name="codePackageName">The name of the code package defined in the service manifest.</param>
        /// <param name="codePackageVersion">The declared service-manifest version to which this mapping applies.
        /// Application-type and internal rollout versions are not part of the target.</param>
        /// <param name="partitionId">The partition whose mapping is selected. Omit this property for a package-wide
        /// mapping.</param>
        public CodePackagePatchTarget(
            string applicationInstanceId,
            string serviceManifestName,
            string codePackageName,
            string codePackageVersion,
            PartitionId partitionId = default(PartitionId))
        {
            applicationInstanceId.ThrowIfNull(nameof(applicationInstanceId));
            serviceManifestName.ThrowIfNull(nameof(serviceManifestName));
            codePackageName.ThrowIfNull(nameof(codePackageName));
            codePackageVersion.ThrowIfNull(nameof(codePackageVersion));
            this.ApplicationInstanceId = applicationInstanceId;
            this.ServiceManifestName = serviceManifestName;
            this.CodePackageName = codePackageName;
            this.CodePackageVersion = codePackageVersion;
            this.PartitionId = partitionId;
        }

        /// <summary>
        /// Gets the opaque identity of one application creation. Deleting and recreating the same application name produces a
        /// different identity.
        /// </summary>
        public string ApplicationInstanceId { get; }

        /// <summary>
        /// Gets the name of the service manifest.
        /// </summary>
        public string ServiceManifestName { get; }

        /// <summary>
        /// Gets the name of the code package defined in the service manifest.
        /// </summary>
        public string CodePackageName { get; }

        /// <summary>
        /// Gets the declared service-manifest version to which this mapping applies. Application-type and internal rollout
        /// versions are not part of the target.
        /// </summary>
        public string CodePackageVersion { get; }

        /// <summary>
        /// Gets the partition whose mapping is selected. Omit this property for a package-wide mapping.
        /// </summary>
        public PartitionId PartitionId { get; }
    }
}
