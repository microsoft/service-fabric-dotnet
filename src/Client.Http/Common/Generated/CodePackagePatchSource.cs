// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One complete replacement code package, not a set of platform variants or a changed-file delta.
    /// This source object identifies stored or provisioned contents; it does not carry a caller-local path or inline file
    /// bytes. Upload a local directory using existing Image Store operations before submitting Create with the resulting
    /// Image Store-relative path.
    /// Service Fabric obtains or computes the replacement checksum during preparation using its normal package validation
    /// policy and retains it for node downloads. The caller does not supply a checksum.
    /// Service Fabric does not select different binaries for different nodes or certify that arbitrary replacement code is
    /// compatible with the application.
    /// </summary>
    public abstract partial class CodePackagePatchSource
    {
        /// <summary>
        /// Initializes a new instance of the CodePackagePatchSource class.
        /// </summary>
        /// <param name="kind">The source from which replacement code is published to the owning application's patch
        /// folder.</param>
        protected CodePackagePatchSource(
            CodePackagePatchSourceKind? kind)
        {
            kind.ThrowIfNull(nameof(kind));
            this.Kind = kind;
        }

        /// <summary>
        /// Gets the source from which replacement code is published to the owning application's patch folder.
        /// </summary>
        public CodePackagePatchSourceKind? Kind { get; }
    }
}
