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
    /// Converter for <see cref="CodePackagePatchState" />.
    /// </summary>
    internal class CodePackagePatchStateConverter
    {
        /// <summary>
        /// Gets the enum value by reading string value from reader.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from, reader must be placed at first property.</param>
        /// <returns>The enum Value.</returns>
        public static CodePackagePatchState? Deserialize(JsonReader reader)
        {
            var value = reader.ReadValueAsString();
            var obj = default(CodePackagePatchState);

            if (string.Compare(value, "Unknown", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchState.Unknown;
            }
            else if (string.Compare(value, "Active", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchState.Active;
            }
            else if (string.Compare(value, "Clearing", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchState.Clearing;
            }
            else if (string.Compare(value, "Cleared", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchState.Cleared;
            }

            return obj;
        }

        /// <summary>
        /// Serializes the enum value.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="value">The object to serialize to JSON.</param>
        public static void Serialize(JsonWriter writer, CodePackagePatchState? value)
        {
            switch (value)
            {
                case CodePackagePatchState.Unknown:
                    writer.WriteStringValue("Unknown");
                    break;
                case CodePackagePatchState.Active:
                    writer.WriteStringValue("Active");
                    break;
                case CodePackagePatchState.Clearing:
                    writer.WriteStringValue("Clearing");
                    break;
                case CodePackagePatchState.Cleared:
                    writer.WriteStringValue("Cleared");
                    break;
                default:
                    throw new ArgumentException($"Invalid value {value.ToString()} for enum type CodePackagePatchState");
            }
        }
    }
}
