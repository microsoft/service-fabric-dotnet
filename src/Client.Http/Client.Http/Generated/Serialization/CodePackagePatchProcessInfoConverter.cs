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
    /// Converter for <see cref="CodePackagePatchProcessInfo" />.
    /// </summary>
    internal class CodePackagePatchProcessInfoConverter
    {
        /// <summary>
        /// Deserializes the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchProcessInfo Deserialize(JsonReader reader)
        {
            return reader.Deserialize(GetFromJsonProperties);
        }

        /// <summary>
        /// Gets the object from Json properties.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from, reader must be placed at first property.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchProcessInfo GetFromJsonProperties(JsonReader reader)
        {
            var codePackageInstanceId = default(string);
            var codePackageVersion = default(string);
            var partitionIds = default(IEnumerable<Guid?>);
            var isRunning = default(bool?);
            var effectiveCodePath = default(string);
            var effectiveContentId = default(string);

            do
            {
                var propName = reader.ReadPropertyName();
                if (string.Compare("CodePackageInstanceId", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    codePackageInstanceId = reader.ReadValueAsString();
                }
                else if (string.Compare("CodePackageVersion", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    codePackageVersion = reader.ReadValueAsString();
                }
                else if (string.Compare("PartitionIds", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    partitionIds = reader.ReadList(JsonReaderExtensions.ReadValueAsGuid);
                }
                else if (string.Compare("IsRunning", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    isRunning = reader.ReadValueAsBool();
                }
                else if (string.Compare("EffectiveCodePath", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    effectiveCodePath = reader.ReadValueAsString();
                }
                else if (string.Compare("EffectiveContentId", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    effectiveContentId = reader.ReadValueAsString();
                }
                else
                {
                    reader.SkipPropertyValue();
                }
            }
            while (reader.TokenType != JsonToken.EndObject);

            return new CodePackagePatchProcessInfo(
                codePackageInstanceId: codePackageInstanceId,
                codePackageVersion: codePackageVersion,
                partitionIds: partitionIds,
                isRunning: isRunning,
                effectiveCodePath: effectiveCodePath,
                effectiveContentId: effectiveContentId);
        }

        /// <summary>
        /// Serializes the object to JSON.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="obj">The object to serialize to JSON.</param>
        internal static void Serialize(JsonWriter writer, CodePackagePatchProcessInfo obj)
        {
            // Required properties are always serialized, optional properties are serialized when not null.
            writer.WriteStartObject();
            writer.WriteProperty(obj.CodePackageInstanceId, "CodePackageInstanceId", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.CodePackageVersion, "CodePackageVersion", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.IsRunning, "IsRunning", JsonWriterExtensions.WriteBoolValue);
            if (obj.PartitionIds != null)
            {
                writer.WriteEnumerableProperty(obj.PartitionIds, "PartitionIds", (w, v) => writer.WriteGuidValue(v));
            }

            if (obj.EffectiveCodePath != null)
            {
                writer.WriteProperty(obj.EffectiveCodePath, "EffectiveCodePath", JsonWriterExtensions.WriteStringValue);
            }

            if (obj.EffectiveContentId != null)
            {
                writer.WriteProperty(obj.EffectiveContentId, "EffectiveContentId", JsonWriterExtensions.WriteStringValue);
            }

            writer.WriteEndObject();
        }
    }
}
