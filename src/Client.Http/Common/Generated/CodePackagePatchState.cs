// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    /// <summary>
    /// Defines values for CodePackagePatchState.
    /// </summary>
    public enum CodePackagePatchState
    {
        /// <summary>
        /// The mapping state is unavailable or not understood by this client. This is not a persisted state and must not be
        /// treated as an absent or cleared patch.
        /// </summary>
        Unknown,

        /// <summary>
        /// The desired mapping is saved. Download, validation, and process work may still be pending.
        /// </summary>
        Active,

        /// <summary>
        /// The mapping is excluded from selection and removal-related process work remains.
        /// </summary>
        Clearing,

        /// <summary>
        /// The mapping is not selected and removal work is complete. Its revision marker is retained to reject delayed
        /// requests.
        /// </summary>
        Cleared,
    }
}
