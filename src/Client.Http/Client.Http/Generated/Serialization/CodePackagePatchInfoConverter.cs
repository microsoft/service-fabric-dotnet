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
    /// Converter for <see cref="CodePackagePatchInfo" />.
    /// </summary>
    internal class CodePackagePatchInfoConverter
    {
        /// <summary>
        /// Deserializes the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchInfo Deserialize(JsonReader reader)
        {
            return reader.Deserialize(GetFromJsonProperties);
        }

        /// <summary>
        /// Gets the object from Json properties.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from, reader must be placed at first property.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchInfo GetFromJsonProperties(JsonReader reader)
        {
            var patchId = default(string);
            var target = default(CodePackagePatchTarget);
            var revision = default(string);
            var state = default(CodePackagePatchState?);
            var contentId = default(string);
            var imageStoreRelativePath = default(string);
            var contentReady = default(bool?);
            var progress = default(CodePackagePatchProgress?);
            var processes = default(IEnumerable<CodePackagePatchProcessInfo>);
            var lastError = default(FabricErrorError);

            do
            {
                var propName = reader.ReadPropertyName();
                if (string.Compare("PatchId", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    patchId = reader.ReadValueAsString();
                }
                else if (string.Compare("Target", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    target = CodePackagePatchTargetConverter.Deserialize(reader);
                }
                else if (string.Compare("Revision", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    revision = reader.ReadValueAsString();
                }
                else if (string.Compare("State", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    state = CodePackagePatchStateConverter.Deserialize(reader);
                }
                else if (string.Compare("ContentId", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    contentId = reader.ReadValueAsString();
                }
                else if (string.Compare("ImageStoreRelativePath", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    imageStoreRelativePath = reader.ReadValueAsString();
                }
                else if (string.Compare("ContentReady", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    contentReady = reader.ReadValueAsBool();
                }
                else if (string.Compare("Progress", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    progress = CodePackagePatchProgressConverter.Deserialize(reader);
                }
                else if (string.Compare("Processes", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    processes = reader.ReadList(CodePackagePatchProcessInfoConverter.Deserialize);
                }
                else if (string.Compare("LastError", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    lastError = FabricErrorErrorConverter.Deserialize(reader);
                }
                else
                {
                    reader.SkipPropertyValue();
                }
            }
            while (reader.TokenType != JsonToken.EndObject);

            return new CodePackagePatchInfo(
                patchId: patchId,
                target: target,
                revision: revision,
                state: state,
                contentId: contentId,
                imageStoreRelativePath: imageStoreRelativePath,
                contentReady: contentReady,
                progress: progress,
                processes: processes,
                lastError: lastError);
        }

        /// <summary>
        /// Serializes the object to JSON.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="obj">The object to serialize to JSON.</param>
        internal static void Serialize(JsonWriter writer, CodePackagePatchInfo obj)
        {
            // Required properties are always serialized, optional properties are serialized when not null.
            writer.WriteStartObject();
            writer.WriteProperty(obj.State, "State", CodePackagePatchStateConverter.Serialize);
            writer.WriteProperty(obj.PatchId, "PatchId", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.Target, "Target", CodePackagePatchTargetConverter.Serialize);
            writer.WriteProperty(obj.Revision, "Revision", JsonWriterExtensions.WriteStringValue);
            writer.WriteProperty(obj.Progress, "Progress", CodePackagePatchProgressConverter.Serialize);
            if (obj.ContentId != null)
            {
                writer.WriteProperty(obj.ContentId, "ContentId", JsonWriterExtensions.WriteStringValue);
            }

            if (obj.ImageStoreRelativePath != null)
            {
                writer.WriteProperty(obj.ImageStoreRelativePath, "ImageStoreRelativePath", JsonWriterExtensions.WriteStringValue);
            }

            if (obj.ContentReady != null)
            {
                writer.WriteProperty(obj.ContentReady, "ContentReady", JsonWriterExtensions.WriteBoolValue);
            }

            if (obj.Processes != null)
            {
                writer.WriteEnumerableProperty(obj.Processes, "Processes", CodePackagePatchProcessInfoConverter.Serialize);
            }

            if (obj.LastError != null)
            {
                writer.WriteProperty(obj.LastError, "LastError", FabricErrorErrorConverter.Serialize);
            }

            writer.WriteEndObject();
        }
    }
}
