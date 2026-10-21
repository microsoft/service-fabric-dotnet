// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Publishes replacement contents and requests a new or replacement mapping on the selected nodes.
    /// Service Fabric resolves the version-qualified target, observes each node's mapping revision, and prepares the node
    /// requests internally. Callers do not supply revision or checksum metadata.
    /// This request replaces the mapping at the selected code package version and scope. Mappings for other versions are
    /// independent. It does not create a stack of prior patches at one target.
    /// </summary>
    public partial class CodePackagePatchCreateDescription
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchCreateDescription class.
        /// </summary>
        /// <param name="serviceName">The service used to identify the application instance and deployed code package.
        /// Package-wide scope can affect other services using that package.</param>
        /// <param name="codePackageName">The name of the code package defined in the service manifest.</param>
        /// <param name="codePackageVersion">The declared service-manifest version of the original code package to patch, not
        /// the replacement source version or internal rollout version.</param>
        /// <param name="source">One complete replacement code package for this Create. Service Fabric uses the same contents
        /// for every selected node.
        /// The caller is responsible for compatibility with the selected nodes and existing launch settings. Use separate
        /// Create operations when different contents are needed.
        /// </param>
        /// <param name="partitionId">Optional partition scope. Omit for package-wide scope. The partition must belong to
        /// ServiceName and use exclusive-process activation.</param>
        /// <param name="contentName">An optional safe name prefix for a new publication's ContentId. Service Fabric appends a
        /// generated unique identifier. Omit for a generated identifier alone. Create does not apply this prefix or rename
        /// content already in the target application's retained HotPatches layout, whether newly uploaded or previously
        /// published.</param>
        /// <param name="nodes">Nodes to target. Omit this property to resolve all currently-up nodes once for this call. An
        /// explicitly empty list is invalid. Duplicate names are removed.
        /// Use an explicit list when retrying so the recipient set does not change between attempts.
        /// </param>
        public CodePackagePatchCreateDescription(
            ServiceName serviceName,
            string codePackageName,
            string codePackageVersion,
            CodePackagePatchSource source,
            PartitionId partitionId = default(PartitionId),
            string contentName = default(string),
            IEnumerable<string> nodes = default(IEnumerable<string>))
        {
            serviceName.ThrowIfNull(nameof(serviceName));
            codePackageName.ThrowIfNull(nameof(codePackageName));
            codePackageVersion.ThrowIfNull(nameof(codePackageVersion));
            source.ThrowIfNull(nameof(source));
            this.ServiceName = serviceName;
            this.CodePackageName = codePackageName;
            this.CodePackageVersion = codePackageVersion;
            this.Source = source;
            this.PartitionId = partitionId;
            this.ContentName = contentName;
            this.Nodes = nodes;
        }

        /// <summary>
        /// Gets the service used to identify the application instance and deployed code package. Package-wide scope can affect
        /// other services using that package.
        /// </summary>
        public ServiceName ServiceName { get; }

        /// <summary>
        /// Gets the name of the code package defined in the service manifest.
        /// </summary>
        public string CodePackageName { get; }

        /// <summary>
        /// Gets the declared service-manifest version of the original code package to patch, not the replacement source
        /// version or internal rollout version.
        /// </summary>
        public string CodePackageVersion { get; }

        /// <summary>
        /// Gets optional partition scope. Omit for package-wide scope. The partition must belong to ServiceName and use
        /// exclusive-process activation.
        /// </summary>
        public PartitionId PartitionId { get; }

        /// <summary>
        /// Gets one complete replacement code package for this Create. Service Fabric uses the same contents for every
        /// selected node.
        /// The caller is responsible for compatibility with the selected nodes and existing launch settings. Use separate
        /// Create operations when different contents are needed.
        /// </summary>
        public CodePackagePatchSource Source { get; }

        /// <summary>
        /// Gets an optional safe name prefix for a new publication's ContentId. Service Fabric appends a generated unique
        /// identifier. Omit for a generated identifier alone. Create does not apply this prefix or rename content already in
        /// the target application's retained HotPatches layout, whether newly uploaded or previously published.
        /// </summary>
        public string ContentName { get; }

        /// <summary>
        /// Gets nodes to target. Omit this property to resolve all currently-up nodes once for this call. An explicitly empty
        /// list is invalid. Duplicate names are removed.
        /// Use an explicit list when retrying so the recipient set does not change between attempts.
        /// </summary>
        public IEnumerable<string> Nodes { get; }
    }
}
