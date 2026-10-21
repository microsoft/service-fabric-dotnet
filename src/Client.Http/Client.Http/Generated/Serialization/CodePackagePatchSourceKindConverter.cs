// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Client.Http.Serialization
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ServiceFabric.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Converter for <see cref="CodePackagePatchSourceKind" />.
    /// </summary>
    internal class CodePackagePatchSourceKindConverter
    {
        /// <summary>
        /// Gets the enum value by reading string value from reader.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from, reader must be placed at first property.</param>
        /// <returns>The enum Value.</returns>
        public static CodePackagePatchSourceKind? Deserialize(JsonReader reader)
        {
            var value = reader.ReadValueAsString();
            var obj = default(CodePackagePatchSourceKind);

            if (string.Compare(value, "ImageStore", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchSourceKind.ImageStore;
            }
            else if (string.Compare(value, "ProvisionedCodePackage", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchSourceKind.ProvisionedCodePackage;
            }

            return obj;
        }

        /// <summary>
        /// Serializes the enum value.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="value">The object to serialize to JSON.</param>
        public static void Serialize(JsonWriter writer, CodePackagePatchSourceKind? value)
        {
            switch (value)
            {
                case CodePackagePatchSourceKind.ImageStore:
                    writer.WriteStringValue("ImageStore");
                    break;
                case CodePackagePatchSourceKind.ProvisionedCodePackage:
                    writer.WriteStringValue("ProvisionedCodePackage");
                    break;
                default:
                    throw new ArgumentException($"Invalid value {value.ToString()} for enum type CodePackagePatchSourceKind");
            }
        }
    }
}
