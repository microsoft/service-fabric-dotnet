// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using Microsoft.ServiceFabric.Common;
using Newtonsoft.Json;

namespace Microsoft.ServiceFabric.Client.Http.Serialization;

static class ExternalRepairTargetDescriptionConverter
{
    internal static ExternalRepairTargetDescription GetFromJsonProperties(JsonReader reader)
    {
        while (reader.TokenType != JsonToken.EndObject)
        {
            _ = reader.ReadPropertyName();
            reader.SkipPropertyValue();
        }

        return new ExternalRepairTargetDescription();
    }

    internal static void Serialize(JsonWriter writer, ExternalRepairTargetDescription obj)
    {
        writer.WriteStartObject();
        writer.WriteProperty(obj.Kind, "Kind", RepairTargetKindConverter.Serialize);
        writer.WriteEndObject();
    }
}
