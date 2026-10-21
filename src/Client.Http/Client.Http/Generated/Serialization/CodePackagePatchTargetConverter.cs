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
    /// Converter for <see cref="CodePackagePatchTarget" />.
    /// </summary>
    internal class CodePackagePatchTargetConverter
    {
        /// <summary>
        /// Deserializes the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchTarget Deserialize(JsonReader reader)
        {
            return reader.Deserialize(GetFromJsonProperties);
        }

        /// <summary>
        /// Gets the object from Json properties.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from, reader must be placed at first property.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchTarget GetFromJsonProperties(JsonReader reader)
        {
            var applicationInstanceId = default(string);
            var serviceManifestName = default(string);
            var codePackageName = default(string);
            var codePackageVersion = default(string);
            var partitionId = default(PartitionId);

            do
            {
                var propName = reader.ReadPropertyName();
                if (string.Compare("ApplicationInstanceId", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    applicationInstanceId = reader.ReadValueAsString();
                }
                else if (string.Compare("ServiceManifestName", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    serviceManifestName = reader.ReadValueAsString();
                }
                else if (string.Compare("CodePackageName", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    codePackageName = reader.ReadValueAsString();
                }
                else if (string.Compare("CodePackageVersion", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    codePackageVersion = reader.ReadValueAsString();
                }
                else if (string.Compare("PartitionId", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    partitionId = PartitionIdConverter.Deserialize(reader);
                }
                else
                {
                    reader.SkipPropertyValue();
                }
            }
            while (reader.TokenType != JsonToken.EndObject);

            return new CodePackagePatchTarget(
                applicationInstanceId: applicationInstanceId,
                serviceManifestName: serviceManifestName,
                codePackageName: codePackageName,
                codePackageVersion: codePackageVersion,
                partitionId: partitionId);
        }

        /// <summary>
        /// Serializes the object to JSON.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="obj">The object to serialize to JSON.</param>
        internal static void Serialize(JsonWriter writer, CodePackagePatchTarget obj)
        {
            // Required properties are always serialized, optional properties are serialized when not null.
            writer.WriteStartObject();
            writer.WriteProperty(obj.ApplicationInstanceId, "ApplicationInstanceId", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.ServiceManifestName, "ServiceManifestName", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.CodePackageName, "CodePackageName", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.CodePackageVersion, "CodePackageVersion", JsonWriterExtensions.WriteStringValue);
            if (obj.PartitionId != null)
            {
                writer.WriteProperty(obj.PartitionId, "PartitionId", PartitionIdConverter.Serialize);
            }

            writer.WriteEndObject();
        }
    }
}
