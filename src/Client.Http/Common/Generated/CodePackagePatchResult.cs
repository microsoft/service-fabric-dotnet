// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Independent outcomes for every selected node. Each selected node appears in exactly one collection.
    /// For Create and Delete, success means durably accepted intent, not completed download or process work. A failure on
    /// one node does not undo acceptance elsewhere, and an unresolved request may still be accepted later.
    /// For Get, success means a conclusive node-local observation. Results are not a cluster-wide atomic snapshot or a
    /// historical audit log.
    /// </summary>
    public partial class CodePackagePatchResult
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchResult class.
        /// </summary>
        /// <param name="successfulNodes">Successful responses, one per node. Create returns its accepted mapping. Delete
        /// returns the saved removal observation, including a revision marker when the target had no previous mapping.
        /// Get returns the matching saved mappings, or an empty Patches list for a reachable node with no matches. No outcome
        /// is inferred for unanswered nodes.
        /// </param>
        /// <param name="failedNodes">Nodes that conclusively rejected this attempt or returned a query error. This includes
        /// failed internal target, authorization, or revision checks.
        /// This does not establish the outcome of an earlier unanswered mutation. No mapping or process state is inferred for
        /// these nodes.
        /// </param>
        /// <param name="unresolvedNodes">Nodes for which no conclusive response was obtained. These are not empty mapping
        /// results, and mutation acceptance may still occur later.</param>
        /// <param name="patchId">The stable target identifier returned in every Create and Delete response, including partial
        /// or unresolved outcomes. Delete echoes the requested identifier. Get reports identifiers on its individual mapping
        /// observations instead.</param>
        /// <param name="contentId">The immutable published replacement identifier, independent of PatchId. Present in every
        /// Create result after successful publication, even if every node rejected the request or has an unresolved outcome.
        /// Not returned at the root of Delete or Get results.</param>
        /// <param name="imageStoreRelativePath">The complete retained path under
        /// Store/{ApplicationType}/HotPatches/{ContentId}. Present in every Create result after successful publication and
        /// reusable as Source.ImageStoreRelativePath in a later Create. Not returned at the root of Delete or Get
        /// results.</param>
        public CodePackagePatchResult(
            IEnumerable<CodePackagePatchNodeInfo> successfulNodes,
            IEnumerable<CodePackagePatchNodeError> failedNodes,
            IEnumerable<CodePackagePatchNodeError> unresolvedNodes,
            string patchId = default(string),
            string contentId = default(string),
            string imageStoreRelativePath = default(string))
        {
            successfulNodes.ThrowIfNull(nameof(successfulNodes));
            failedNodes.ThrowIfNull(nameof(failedNodes));
            unresolvedNodes.ThrowIfNull(nameof(unresolvedNodes));
            this.SuccessfulNodes = successfulNodes;
            this.FailedNodes = failedNodes;
            this.UnresolvedNodes = unresolvedNodes;
            this.PatchId = patchId;
            this.ContentId = contentId;
            this.ImageStoreRelativePath = imageStoreRelativePath;
        }

        /// <summary>
        /// Gets the stable target identifier returned in every Create and Delete response, including partial or unresolved
        /// outcomes. Delete echoes the requested identifier. Get reports identifiers on its individual mapping observations
        /// instead.
        /// </summary>
        public string PatchId { get; }

        /// <summary>
        /// Gets the immutable published replacement identifier, independent of PatchId. Present in every Create result after
        /// successful publication, even if every node rejected the request or has an unresolved outcome. Not returned at the
        /// root of Delete or Get results.
        /// </summary>
        public string ContentId { get; }

        /// <summary>
        /// Gets the complete retained path under Store/{ApplicationType}/HotPatches/{ContentId}. Present in every Create
        /// result after successful publication and reusable as Source.ImageStoreRelativePath in a later Create. Not returned
        /// at the root of Delete or Get results.
        /// </summary>
        public string ImageStoreRelativePath { get; }

        /// <summary>
        /// Gets successful responses, one per node. Create returns its accepted mapping. Delete returns the saved removal
        /// observation, including a revision marker when the target had no previous mapping.
        /// Get returns the matching saved mappings, or an empty Patches list for a reachable node with no matches. No outcome
        /// is inferred for unanswered nodes.
        /// </summary>
        public IEnumerable<CodePackagePatchNodeInfo> SuccessfulNodes { get; }

        /// <summary>
        /// Gets nodes that conclusively rejected this attempt or returned a query error. This includes failed internal target,
        /// authorization, or revision checks.
        /// This does not establish the outcome of an earlier unanswered mutation. No mapping or process state is inferred for
        /// these nodes.
        /// </summary>
        public IEnumerable<CodePackagePatchNodeError> FailedNodes { get; }

        /// <summary>
        /// Gets nodes for which no conclusive response was obtained. These are not empty mapping results, and mutation
        /// acceptance may still occur later.
        /// </summary>
        public IEnumerable<CodePackagePatchNodeError> UnresolvedNodes { get; }
    }
}
