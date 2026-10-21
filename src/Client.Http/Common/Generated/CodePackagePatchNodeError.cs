// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A per-node error whose certainty is determined by the FailedNodes or UnresolvedNodes collection containing it.
    /// </summary>
    public partial class CodePackagePatchNodeError
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchNodeError class.
        /// </summary>
        /// <param name="nodeName">The name of a Service Fabric node.</param>
        /// <param name="error">Error object containing error code and error message.</param>
        public CodePackagePatchNodeError(
            NodeName nodeName,
            FabricErrorError error)
        {
            nodeName.ThrowIfNull(nameof(nodeName));
            error.ThrowIfNull(nameof(error));
            this.NodeName = nodeName;
            this.Error = error;
        }

        /// <summary>
        /// Gets the name of a Service Fabric node.
        /// </summary>
        public NodeName NodeName { get; }

        /// <summary>
        /// Gets error object containing error code and error message.
        /// </summary>
        public FabricErrorError Error { get; }
    }
}
