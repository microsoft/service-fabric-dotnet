// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    /// <summary>
    /// Defines values for CodePackagePatchSourceKind.
    /// </summary>
    public enum CodePackagePatchSourceKind
    {
        /// <summary>
        /// Complete replacement contents at an Image Store-relative path, including a reusable retained hot-patch publication.
        /// </summary>
        ImageStore,

        /// <summary>
        /// A code package belonging to a provisioned application type and service manifest.
        /// </summary>
        ProvisionedCodePackage,
    }
}
