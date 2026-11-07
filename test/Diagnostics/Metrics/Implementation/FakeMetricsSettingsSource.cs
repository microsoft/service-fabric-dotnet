// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// In-memory configuration and environment for <see cref="MetricsSettings"/> and <see cref="MetricsBackendSelector"/>.
/// </summary>
sealed class FakeMetricsSettingsSource : IMetricsSettingsSource
{
    internal const string HostName = "TestHost";
    internal const string FabricVersion = "9.8.7.6";

    readonly Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> environment = new(StringComparer.Ordinal);

    internal FakeMetricsSettingsSource Set(string sectionName, string keyName, string value)
    {
        if (!sections.TryGetValue(sectionName, out Dictionary<string, string> section))
            sections[sectionName] = section = new Dictionary<string, string>(StringComparer.Ordinal);

        section[keyName] = value;
        return this;
    }

    internal FakeMetricsSettingsSource SetEnvironmentVariable(string name, string value)
    {
        environment[name] = value;
        return this;
    }

    internal FakeMetricsSettingsSource SetCollectors(params string[] connectionStrings) =>
        Set(MetricsSettings.CollectorsSection, "Geneva", "{\"ConnectionStrings\":[" + string.Join(",", connectionStrings.Select(c => "\"" + c + "\"")) + "]}");

    string IMetricsSettingsSource.ReadConfigValue(string sectionName, string keyName) =>
        sections.TryGetValue(sectionName, out Dictionary<string, string> section) && section.TryGetValue(keyName, out string value) ? value : string.Empty;

    IEnumerable<string> IMetricsSettingsSource.GetConfigKeys(string sectionName) =>
        sections.TryGetValue(sectionName, out Dictionary<string, string> section) ? section.Keys.ToArray() : Array.Empty<string>();

    string IMetricsSettingsSource.GetEnvironmentVariable(string name) =>
        environment.TryGetValue(name, out string value) ? value : null;

    string IMetricsSettingsSource.GetHostName() => HostName;

    string IMetricsSettingsSource.GetFabricVersion() => FabricVersion;
}
