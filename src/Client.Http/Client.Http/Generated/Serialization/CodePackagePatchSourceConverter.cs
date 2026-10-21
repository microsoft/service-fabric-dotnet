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
    /// Converter for <see cref="CodePackagePatchSource" />.
    /// </summary>
    internal class CodePackagePatchSourceConverter
    {
        /// <summary>
        /// Deserializes the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchSource Deserialize(JsonReader reader)
        {
            return reader.Deserialize(GetFromJsonProperties);
        }

        /// <summary>
        /// Gets the object from Json properties.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <returns>The object Value.</returns>
        internal static CodePackagePatchSource GetFromJsonProperties(JsonReader reader)
        {
            CodePackagePatchSource obj = null;
            var propName = reader.ReadPropertyName();
            if (!propName.Equals("Kind", StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonReaderException($"Incorrect discriminator property name {propName}, Expected discriminator property name is Kind.");
            }

            var propValue = reader.ReadValueAsString();
            if (propValue.Equals("ImageStore", StringComparison.OrdinalIgnoreCase))
            {
                obj = ImageStoreCodePackagePatchSourceConverter.GetFromJsonProperties(reader);
            }
            else if (propValue.Equals("ProvisionedCodePackage", StringComparison.OrdinalIgnoreCase))
            {
                obj = ProvisionedCodePackagePatchSourceConverter.GetFromJsonProperties(reader);
            }
            else
            {
                throw new InvalidOperationException("Unknown Kind.");
            }

            return obj;
        }

        /// <summary>
        /// Serializes the object to JSON.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="obj">The object to serialize to JSON.</param>
        internal static void Serialize(JsonWriter writer, CodePackagePatchSource obj)
        {
            var kind = obj.Kind;
            if (kind.Equals(CodePackagePatchSourceKind.ImageStore))
            {
                ImageStoreCodePackagePatchSourceConverter.Serialize(writer, (ImageStoreCodePackagePatchSource)obj);
            }
            else if (kind.Equals(CodePackagePatchSourceKind.ProvisionedCodePackage))
            {
                ProvisionedCodePackagePatchSourceConverter.Serialize(writer, (ProvisionedCodePackagePatchSource)obj);
            }
            else
            {
                throw new InvalidOperationException("Unknown Kind.");
            }
        }
    }
}
