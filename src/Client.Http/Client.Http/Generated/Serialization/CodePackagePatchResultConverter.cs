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
    /// Converter for <see cref="CodePackagePatchResult" />.
    /// </summary>
    internal class CodePackagePatchResultConverter
    {
        /// <summary>
        /// Deserializes the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchResult Deserialize(JsonReader reader)
        {
            return reader.Deserialize(GetFromJsonProperties);
        }

        /// <summary>
        /// Gets the object from Json properties.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from, reader must be placed at first property.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchResult GetFromJsonProperties(JsonReader reader)
        {
            var patchId = default(string);
            var contentId = default(string);
            var imageStoreRelativePath = default(string);
            var successfulNodes = default(IEnumerable<CodePackagePatchNodeInfo>);
            var failedNodes = default(IEnumerable<CodePackagePatchNodeError>);
            var unresolvedNodes = default(IEnumerable<CodePackagePatchNodeError>);

            do
            {
                var propName = reader.ReadPropertyName();
                if (string.Compare("PatchId", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    patchId = reader.ReadValueAsString();
                }
                else if (string.Compare("ContentId", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    contentId = reader.ReadValueAsString();
                }
                else if (string.Compare("ImageStoreRelativePath", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    imageStoreRelativePath = reader.ReadValueAsString();
                }
                else if (string.Compare("SuccessfulNodes", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    successfulNodes = reader.ReadList(CodePackagePatchNodeInfoConverter.Deserialize);
                }
                else if (string.Compare("FailedNodes", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    failedNodes = reader.ReadList(CodePackagePatchNodeErrorConverter.Deserialize);
                }
                else if (string.Compare("UnresolvedNodes", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    unresolvedNodes = reader.ReadList(CodePackagePatchNodeErrorConverter.Deserialize);
                }
                else
                {
                    reader.SkipPropertyValue();
                }
            }
            while (reader.TokenType != JsonToken.EndObject);

            return new CodePackagePatchResult(
                patchId: patchId,
                contentId: contentId,
                imageStoreRelativePath: imageStoreRelativePath,
                successfulNodes: successfulNodes,
                failedNodes: failedNodes,
                unresolvedNodes: unresolvedNodes);
        }

        /// <summary>
        /// Serializes the object to JSON.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="obj">The object to serialize to JSON.</param>
        internal static void Serialize(JsonWriter writer, CodePackagePatchResult obj)
        {
            // Required properties are always serialized, optional properties are serialized when not null.
            writer.WriteStartObject();
            writer.WriteEnumerableProperty(obj.SuccessfulNodes, "SuccessfulNodes", CodePackagePatchNodeInfoConverter.Serialize);
            writer.WriteEnumerableProperty(obj.FailedNodes, "FailedNodes", CodePackagePatchNodeErrorConverter.Serialize);
            writer.WriteEnumerableProperty(obj.UnresolvedNodes, "UnresolvedNodes", CodePackagePatchNodeErrorConverter.Serialize);
            if (obj.PatchId != null)
            {
                writer.WriteProperty(obj.PatchId, "PatchId", JsonWriterExtensions.WriteStringValue);
            }

            if (obj.ContentId != null)
            {
                writer.WriteProperty(obj.ContentId, "ContentId", JsonWriterExtensions.WriteStringValue);
            }

            if (obj.ImageStoreRelativePath != null)
            {
                writer.WriteProperty(obj.ImageStoreRelativePath, "ImageStoreRelativePath", JsonWriterExtensions.WriteStringValue);
            }

            writer.WriteEndObject();
        }
    }
}
