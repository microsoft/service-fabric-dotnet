// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Replacement code-package contents at an Image Store-relative path.
    /// Newly uploaded contents already at Store/{ApplicationType}/HotPatches/{ContentId} for the target application type
    /// are validated and finalized in place, preserving that ContentId and path without another copy. ContentName is not
    /// reapplied.
    /// A completed retained publication owned by the same application type reuses its ContentId and path without another
    /// upload or copy.
    /// Other sources, including publications owned by another application type, are copied under
    /// Store/{ApplicationType}/HotPatches/{ContentId} for the target type before node submission.
    /// The source must remain unchanged until publication completes. It is not a local path, URI, or credential-bearing
    /// connection string.
    /// </summary>
    public partial class ImageStoreCodePackagePatchSource : CodePackagePatchSource
    {
        /// <summary>
        /// Initializes a new instance of the ImageStoreCodePackagePatchSource class.
        /// </summary>
        /// <param name="imageStoreRelativePath">The Image Store-relative directory containing the complete replacement code
        /// package.</param>
        public ImageStoreCodePackagePatchSource(
            string imageStoreRelativePath)
            : base(
                Common.CodePackagePatchSourceKind.ImageStore)
        {
            imageStoreRelativePath.ThrowIfNull(nameof(imageStoreRelativePath));
            this.ImageStoreRelativePath = imageStoreRelativePath;
        }

        /// <summary>
        /// Gets the Image Store-relative directory containing the complete replacement code package.
        /// </summary>
        public string ImageStoreRelativePath { get; }
    }
}
