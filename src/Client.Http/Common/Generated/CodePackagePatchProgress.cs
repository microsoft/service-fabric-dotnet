// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    /// <summary>
    /// Defines values for CodePackagePatchProgress.
    /// </summary>
    public enum CodePackagePatchProgress
    {
        /// <summary>
        /// No conclusive process or work observation is available.
        /// </summary>
        Unknown,

        /// <summary>
        /// Content has not yet been downloaded. No process is stopped to switch to unavailable content.
        /// </summary>
        DownloadPending,

        /// <summary>
        /// Content is being downloaded, validated, or prepared for the existing execution identities.
        /// </summary>
        PreparingContent,

        /// <summary>
        /// Content is ready but one or more affected processes have not completed the switch.
        /// </summary>
        RestartPending,

        /// <summary>
        /// All affected main processes run the selected replacement. This does not certify application health.
        /// </summary>
        ReplacementRunning,

        /// <summary>
        /// The mapping is saved without a current activation. Content readiness is reported independently.
        /// </summary>
        NoCurrentActivation,

        /// <summary>
        /// A more specific mapping takes precedence for the observed activation.
        /// </summary>
        AnotherMappingSelected,

        /// <summary>
        /// Current activations use a different declared code package version. The mapping remains Active and its content can
        /// be prepared for a future matching activation. This is not an error and does not restart the different-version
        /// processes.
        /// </summary>
        DifferentCodePackageVersion,

        /// <summary>
        /// The mapping is no longer selected but affected processes still need to switch.
        /// </summary>
        RemovalPending,

        /// <summary>
        /// No process work remains for the accepted removal revision.
        /// </summary>
        RemovalComplete,

        /// <summary>
        /// Download, validation, access, or process work failed. The error is reported separately and accepted intent is
        /// retained.
        /// </summary>
        Blocked,
    }
}
