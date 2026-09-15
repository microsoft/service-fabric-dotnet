// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    /// <summary>
    /// Defines values for KeyValueStoreEseFormat.
    /// </summary>
    public enum KeyValueStoreEseFormat
    {
        /// <summary>
        /// The original Ese key value store format.
        /// </summary>
        Legacy,

        /// <summary>
        /// First-generation updated Ese key value store format.
        /// </summary>
        Hop1,

        /// <summary>
        /// Second-generation Ese key value store format with legacy compatibility.
        /// </summary>
        Hop2Legacy,
    }
}
