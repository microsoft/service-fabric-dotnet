// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// The Geneva entry of Telemetry/Metrics/Collectors, for example {"ConnectionStrings":["Account=a;Namespace=n"]}.
/// Same shape as native GenevaCollectorSettings; property names are case-sensitive.
/// </summary>
[DataContract]
sealed class GenevaCollectorSettingsJson
{
    [DataMember(Name = "ConnectionStrings")]
    public string[] ConnectionStrings { get; set; }

    internal static bool TryParse(string json, out IReadOnlyList<string> connectionStrings, out Exception error)
    {
        connectionStrings = [];
        error = null;
        try
        {
            DataContractJsonSerializer serializer = new(typeof(GenevaCollectorSettingsJson));
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(json ?? string.Empty));
            var settings = (GenevaCollectorSettingsJson)serializer.ReadObject(stream);
            connectionStrings = settings?.ConnectionStrings ?? [];
            return true;
        }
        catch (Exception e)
        {
            error = e;
            return false;
        }
    }
}
