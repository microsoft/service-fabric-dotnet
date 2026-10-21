// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Removes the current mapping at the version-qualified target identified by PatchId on the selected nodes without
    /// deleting the shared Image Store package.
    /// Once a node accepts removal, Hosting completes the remaining process work independently of the caller.
    /// Delete is available when EnableHotPatching is disabled and does not require replacement code to become healthy.
    /// Service Fabric decodes the target from the identifier and prepares revision checks internally, including when no
    /// mapping exists. No current service lookup or caller-supplied target is required.
    /// A fresh Delete removes the current selection. Internal retries preserve the original revision checks and cannot
    /// remove a later replacement. Mappings for other code package versions are not removed.
    /// </summary>
    public partial class CodePackagePatchDeleteDescription
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchDeleteDescription class.
        /// </summary>
        /// <param name="nodes">Nodes to target. Omission resolves all currently-up nodes once, which may differ from the
        /// original Create recipients.
        /// An explicitly empty list is invalid. Duplicate names are removed. Delete must eventually reach every accepting or
        /// possibly accepting node.
        /// </param>
        public CodePackagePatchDeleteDescription(
            IEnumerable<string> nodes = default(IEnumerable<string>))
        {
            this.Nodes = nodes;
        }

        /// <summary>
        /// Gets nodes to target. Omission resolves all currently-up nodes once, which may differ from the original Create
        /// recipients.
        /// An explicitly empty list is invalid. Duplicate names are removed. Delete must eventually reach every accepting or
        /// possibly accepting node.
        /// </summary>
        public IEnumerable<string> Nodes { get; }
    }
}
