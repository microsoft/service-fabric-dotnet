// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A successful response containing the mapping observations from one node.
    /// </summary>
    public partial class CodePackagePatchNodeInfo
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchNodeInfo class.
        /// </summary>
        /// <param name="nodeName">The name of a Service Fabric node.</param>
        /// <param name="patches">Create returns one accepted mapping. Delete returns the saved removal observation for its
        /// version-qualified target, including a first-mention revision marker when no mapping existed.
        /// Get returns every retained mapping matching the supplied filters on this node. An empty list explicitly identifies
        /// a successful response with no matching mappings and is distinct from an unanswered query.
        /// </param>
        public CodePackagePatchNodeInfo(
            NodeName nodeName,
            IEnumerable<CodePackagePatchInfo> patches)
        {
            nodeName.ThrowIfNull(nameof(nodeName));
            patches.ThrowIfNull(nameof(patches));
            this.NodeName = nodeName;
            this.Patches = patches;
        }

        /// <summary>
        /// Gets the name of a Service Fabric node.
        /// </summary>
        public NodeName NodeName { get; }

        /// <summary>
        /// Gets create returns one accepted mapping. Delete returns the saved removal observation for its version-qualified
        /// target, including a first-mention revision marker when no mapping existed.
        /// Get returns every retained mapping matching the supplied filters on this node. An empty list explicitly identifies
        /// a successful response with no matching mappings and is distinct from an unanswered query.
        /// </summary>
        public IEnumerable<CodePackagePatchInfo> Patches { get; }
    }
}
