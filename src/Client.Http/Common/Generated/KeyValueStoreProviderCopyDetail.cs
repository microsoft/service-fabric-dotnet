// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Base type for provider-specific key value store copy details during replica build.
    /// The Kind property determines the storage provider for this copy detail.
    /// </summary>
    public abstract partial class KeyValueStoreProviderCopyDetail
    {
        /// <summary>
        /// Initializes a new instance of the KeyValueStoreProviderCopyDetail class.
        /// </summary>
        /// <param name="kind">The storage provider backing the key value store replica. Determines which copy detail
        /// type is populated during replica build.
        /// </param>
        protected KeyValueStoreProviderCopyDetail(
            KeyValueStoreProviderKind? kind)
        {
            kind.ThrowIfNull(nameof(kind));
            this.Kind = kind;
        }

        /// <summary>
        /// Gets the storage provider backing the key value store replica. Determines which copy detail
        /// type is populated during replica build.
        /// </summary>
        public KeyValueStoreProviderKind? Kind { get; }
    }
}
