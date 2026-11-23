// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using Microsoft.ServiceFabric.Common;
using Newtonsoft.Json;

namespace Microsoft.ServiceFabric.Client.Http.Serialization;

static class ExternalRepairImpactDescriptionConverter
{
    internal static ExternalRepairImpactDescription GetFromJsonProperties(JsonReader reader)
    {
        ExternalRepairImpactDescription impact = null;
        while (reader.TokenType != JsonToken.EndObject)
        {
            string property = reader.ReadPropertyName();
            if (string.Equals(property, "ExternalImpactInfo", StringComparison.OrdinalIgnoreCase))
                impact = reader.Deserialize(ReadImpactInfo);
            else
                reader.SkipPropertyValue();
        }

        return impact ?? throw new JsonReaderException("ExternalImpactInfo is required for an External repair impact.");
    }

    static ExternalRepairImpactDescription ReadImpactInfo(JsonReader reader)
    {
        string level = null;
        while (reader.TokenType != JsonToken.EndObject)
        {
            string property = reader.ReadPropertyName();
            if (string.Equals(property, "ImpactLevel", StringComparison.OrdinalIgnoreCase))
                level = reader.ReadValueAsString();
            else
                reader.SkipPropertyValue();
        }

        try
        {
            return new ExternalRepairImpactDescription(level);
        }
        catch (ArgumentException exception)
        {
            throw new JsonReaderException("Invalid ExternalImpactInfo.ImpactLevel.", exception);
        }
    }

    internal static void Serialize(JsonWriter writer, ExternalRepairImpactDescription obj)
    {
        writer.WriteStartObject();
        writer.WriteProperty(obj.Kind, "Kind", RepairImpactKindConverter.Serialize);
        writer.WritePropertyName("ExternalImpactInfo");
        writer.WriteStartObject();
        writer.WritePropertyName("ImpactLevel");
        writer.WriteValue(obj.ImpactLevel.ToString());
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
