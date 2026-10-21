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
    /// Converter for <see cref="CodePackagePatchCreateDescription" />.
    /// </summary>
    internal class CodePackagePatchCreateDescriptionConverter
    {
        /// <summary>
        /// Deserializes the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchCreateDescription Deserialize(JsonReader reader)
        {
            return reader.Deserialize(GetFromJsonProperties);
        }

        /// <summary>
        /// Gets the object from Json properties.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from, reader must be placed at first property.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchCreateDescription GetFromJsonProperties(JsonReader reader)
        {
            var serviceName = default(ServiceName);
            var codePackageName = default(string);
            var codePackageVersion = default(string);
            var partitionId = default(PartitionId);
            var source = default(CodePackagePatchSource);
            var contentName = default(string);
            var nodes = default(IEnumerable<string>);

            do
            {
                var propName = reader.ReadPropertyName();
                if (string.Compare("ServiceName", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    serviceName = ServiceNameConverter.Deserialize(reader);
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
                else if (string.Compare("Source", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    source = CodePackagePatchSourceConverter.Deserialize(reader);
                }
                else if (string.Compare("ContentName", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    contentName = reader.ReadValueAsString();
                }
                else if (string.Compare("Nodes", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    nodes = reader.ReadList(JsonReaderExtensions.ReadValueAsString);
                }
                else
                {
                    reader.SkipPropertyValue();
                }
            }
            while (reader.TokenType != JsonToken.EndObject);

            return new CodePackagePatchCreateDescription(
                serviceName: serviceName,
                codePackageName: codePackageName,
                codePackageVersion: codePackageVersion,
                partitionId: partitionId,
                source: source,
                contentName: contentName,
                nodes: nodes);
        }

        /// <summary>
        /// Serializes the object to JSON.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="obj">The object to serialize to JSON.</param>
        internal static void Serialize(JsonWriter writer, CodePackagePatchCreateDescription obj)
        {
            // Required properties are always serialized, optional properties are serialized when not null.
            writer.WriteStartObject();
            writer.WriteProperty(obj.ServiceName, "ServiceName", ServiceNameConverter.Serialize);
            writer.WriteProperty(obj.CodePackageName, "CodePackageName", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.CodePackageVersion, "CodePackageVersion", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.Source, "Source", CodePackagePatchSourceConverter.Serialize);
            if (obj.PartitionId != null)
            {
                writer.WriteProperty(obj.PartitionId, "PartitionId", PartitionIdConverter.Serialize);
            }

            if (obj.ContentName != null)
            {
                writer.WriteProperty(obj.ContentName, "ContentName", JsonWriterExtensions.WriteStringValue);
            }

            if (obj.Nodes != null)
            {
                writer.WriteEnumerableProperty(obj.Nodes, "Nodes", (w, v) => writer.WriteStringValue(v));
            }

            writer.WriteEndObject();
        }
    }
}
