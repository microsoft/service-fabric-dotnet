// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The saved mapping and current observations for one exact target on a reachable node.
    /// Mapping state, content readiness, and process use are independent. A saved Active mapping is not proof that
    /// replacement code ran.
    /// </summary>
    public partial class CodePackagePatchInfo
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchInfo class.
        /// </summary>
        /// <param name="patchId">The stable identifier of a resolved application instance, service package, code package name
        /// and declared version, and package or partition scope.
        /// Service Fabric returns a versioned string that identifies the same target across nodes and repeated Create
        /// operations. Content and revision are not part of this identifier.
        /// Pass the returned value to Delete without reconstructing the target. Service Fabric can decode it even when a node
        /// has no saved mapping or the original service no longer exists.
        /// </param>
        /// <param name="target">The immutable application instance, declared code package version, and scope resolved by
        /// Service Fabric.
        /// Use the values returned by GetCodePackagePatchInfoList. The application instance ID is not the encoded
        /// application-name ID used in REST resource paths.
        /// Omitting PartitionId identifies the package-wide mapping. Including PartitionId identifies only that partition's
        /// mapping.
        /// </param>
        /// <param name="revision">The retained unsigned 64-bit mapping revision encoded as a decimal string. Cleared records
        /// retain their revision. This is an observation, not an input required from the caller.</param>
        /// <param name="state">The saved mapping state. Absence is represented by no matching item in a reachable node's
        /// Patches list, not by another persisted state. Possible values include: 'Unknown', 'Active', 'Clearing',
        /// 'Cleared'</param>
        /// <param name="contentId">The immutable publication selected by this mapping. Present for Active and Clearing
        /// mappings; a Cleared marker can omit it. Several targets can reuse one publication, and nodes with the same PatchId
        /// can select different publications.</param>
        /// <param name="imageStoreRelativePath">The retained source path under Store/{ApplicationType}/HotPatches/{ContentId}.
        /// It is derived from the saved publication and owning application type, not the caller's original upload
        /// path.</param>
        /// <param name="contentReady">Whether the selected replacement is locally ready under the normal ImageCache validation
        /// policy. False when content is missing or still being prepared.</param>
        /// <param name="progress">The node's current observation of work for the mapping. This is not a separate persisted
        /// workflow state. Possible values include: 'Unknown', 'DownloadPending', 'PreparingContent', 'RestartPending',
        /// 'ReplacementRunning', 'NoCurrentActivation', 'AnotherMappingSelected', 'DifferentCodePackageVersion',
        /// 'RemovalPending', 'RemovalComplete', 'Blocked'</param>
        /// <param name="processes">Current activation observations, including a different running code package version that
        /// does not select this mapping. An empty list does not imply that the saved mapping was removed.</param>
        /// <param name="lastError">The most recent blocking execution error, when present. This does not change an earlier
        /// successful acceptance into a failed mutation result.</param>
        public CodePackagePatchInfo(
            string patchId,
            CodePackagePatchTarget target,
            string revision,
            CodePackagePatchState? state,
            string contentId = default(string),
            string imageStoreRelativePath = default(string),
            bool? contentReady = default(bool?),
            CodePackagePatchProgress? progress = default(CodePackagePatchProgress?),
            IEnumerable<CodePackagePatchProcessInfo> processes = default(IEnumerable<CodePackagePatchProcessInfo>),
            FabricErrorError lastError = default(FabricErrorError))
        {
            patchId.ThrowIfNull(nameof(patchId));
            target.ThrowIfNull(nameof(target));
            revision.ThrowIfNull(nameof(revision));
            state.ThrowIfNull(nameof(state));
            this.PatchId = patchId;
            this.Target = target;
            this.Revision = revision;
            this.State = state;
            this.ContentId = contentId;
            this.ImageStoreRelativePath = imageStoreRelativePath;
            this.ContentReady = contentReady;
            this.Progress = progress;
            this.Processes = processes;
            this.LastError = lastError;
        }

        /// <summary>
        /// Gets the stable identifier of a resolved application instance, service package, code package name and declared
        /// version, and package or partition scope.
        /// Service Fabric returns a versioned string that identifies the same target across nodes and repeated Create
        /// operations. Content and revision are not part of this identifier.
        /// Pass the returned value to Delete without reconstructing the target. Service Fabric can decode it even when a node
        /// has no saved mapping or the original service no longer exists.
        /// </summary>
        public string PatchId { get; }

        /// <summary>
        /// Gets the immutable application instance, declared code package version, and scope resolved by Service Fabric.
        /// Use the values returned by GetCodePackagePatchInfoList. The application instance ID is not the encoded
        /// application-name ID used in REST resource paths.
        /// Omitting PartitionId identifies the package-wide mapping. Including PartitionId identifies only that partition's
        /// mapping.
        /// </summary>
        public CodePackagePatchTarget Target { get; }

        /// <summary>
        /// Gets the retained unsigned 64-bit mapping revision encoded as a decimal string. Cleared records retain their
        /// revision. This is an observation, not an input required from the caller.
        /// </summary>
        public string Revision { get; }

        /// <summary>
        /// Gets the saved mapping state. Absence is represented by no matching item in a reachable node's Patches list, not by
        /// another persisted state. Possible values include: 'Unknown', 'Active', 'Clearing', 'Cleared'
        /// </summary>
        public CodePackagePatchState? State { get; }

        /// <summary>
        /// Gets the immutable publication selected by this mapping. Present for Active and Clearing mappings; a Cleared marker
        /// can omit it. Several targets can reuse one publication, and nodes with the same PatchId can select different
        /// publications.
        /// </summary>
        public string ContentId { get; }

        /// <summary>
        /// Gets the retained source path under Store/{ApplicationType}/HotPatches/{ContentId}. It is derived from the saved
        /// publication and owning application type, not the caller's original upload path.
        /// </summary>
        public string ImageStoreRelativePath { get; }

        /// <summary>
        /// Gets whether the selected replacement is locally ready under the normal ImageCache validation policy. False when
        /// content is missing or still being prepared.
        /// </summary>
        public bool? ContentReady { get; }

        /// <summary>
        /// Gets the node's current observation of work for the mapping. This is not a separate persisted workflow state.
        /// Possible values include: 'Unknown', 'DownloadPending', 'PreparingContent', 'RestartPending', 'ReplacementRunning',
        /// 'NoCurrentActivation', 'AnotherMappingSelected', 'DifferentCodePackageVersion', 'RemovalPending',
        /// 'RemovalComplete', 'Blocked'
        /// </summary>
        public CodePackagePatchProgress? Progress { get; }

        /// <summary>
        /// Gets current activation observations, including a different running code package version that does not select this
        /// mapping. An empty list does not imply that the saved mapping was removed.
        /// </summary>
        public IEnumerable<CodePackagePatchProcessInfo> Processes { get; }

        /// <summary>
        /// Gets the most recent blocking execution error, when present. This does not change an earlier successful acceptance
        /// into a failed mutation result.
        /// </summary>
        public FabricErrorError LastError { get; }
    }
}
