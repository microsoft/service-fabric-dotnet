// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System.Collections.Generic;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Configuration and environment that metrics settings are read from.
/// </summary>
interface IMetricsSettingsSource
{
    /// <summary>
    /// Returns the configured value, or null or "" when the key is absent.
    /// </summary>
    string ReadConfigValue(string sectionName, string keyName);

    IEnumerable<string> GetConfigKeys(string sectionName);

    /// <summary>
    /// Returns the environment variable's value, or null when it is not set.
    /// </summary>
    string GetEnvironmentVariable(string name);

    string GetHostName();

    /// <summary>
    /// Returns the runtime version used when FabricNode/NodeVersion is not configured (native FabricEnvironment::GetFabricVersion).
    /// </summary>
    string GetFabricVersion();
}
