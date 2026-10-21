// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The observed code package process and effective code selection on a node.
    /// </summary>
    public partial class CodePackagePatchProcessInfo
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchProcessInfo class.
        /// </summary>
        /// <param name="codePackageInstanceId">The instance ID for current running entry point. For a code package setup entry
        /// point (if specified) runs first and after it finishes main entry point is started. Each time entry point executable
        /// is run, its instance id will change.</param>
        /// <param name="codePackageVersion">The declared code package version used by this activation. A different version
        /// does not select the saved mapping and is not an error.</param>
        /// <param name="isRunning">Whether the observed main process is running. A successful start does not establish
        /// application health.</param>
        /// <param name="partitionIds">The partitions associated with this activation. A shared host can contain more than one
        /// partition.</param>
        /// <param name="effectiveCodePath">The node-local code directory selected for this activation. This is an observation,
        /// not an accepted source path for Create.</param>
        /// <param name="effectiveContentId">The immutable replacement publication selected for this activation. Absent when
        /// canonical deployed code is selected. This can differ from the saved mapping's ContentId while a switch is pending
        /// or another mapping takes precedence.</param>
        public CodePackagePatchProcessInfo(
            string codePackageInstanceId,
            string codePackageVersion,
            bool? isRunning,
            IEnumerable<Guid?> partitionIds = default(IEnumerable<Guid?>),
            string effectiveCodePath = default(string),
            string effectiveContentId = default(string))
        {
            codePackageInstanceId.ThrowIfNull(nameof(codePackageInstanceId));
            codePackageVersion.ThrowIfNull(nameof(codePackageVersion));
            isRunning.ThrowIfNull(nameof(isRunning));
            this.CodePackageInstanceId = codePackageInstanceId;
            this.CodePackageVersion = codePackageVersion;
            this.IsRunning = isRunning;
            this.PartitionIds = partitionIds;
            this.EffectiveCodePath = effectiveCodePath;
            this.EffectiveContentId = effectiveContentId;
        }

        /// <summary>
        /// Gets the instance ID for current running entry point. For a code package setup entry point (if specified) runs
        /// first and after it finishes main entry point is started. Each time entry point executable is run, its instance id
        /// will change.
        /// </summary>
        public string CodePackageInstanceId { get; }

        /// <summary>
        /// Gets the declared code package version used by this activation. A different version does not select the saved
        /// mapping and is not an error.
        /// </summary>
        public string CodePackageVersion { get; }

        /// <summary>
        /// Gets the partitions associated with this activation. A shared host can contain more than one partition.
        /// </summary>
        public IEnumerable<Guid?> PartitionIds { get; }

        /// <summary>
        /// Gets whether the observed main process is running. A successful start does not establish application health.
        /// </summary>
        public bool? IsRunning { get; }

        /// <summary>
        /// Gets the node-local code directory selected for this activation. This is an observation, not an accepted source
        /// path for Create.
        /// </summary>
        public string EffectiveCodePath { get; }

        /// <summary>
        /// Gets the immutable replacement publication selected for this activation. Absent when canonical deployed code is
        /// selected. This can differ from the saved mapping's ContentId while a switch is pending or another mapping takes
        /// precedence.
        /// </summary>
        public string EffectiveContentId { get; }
    }
}
