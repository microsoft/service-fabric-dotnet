// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric.Common;
using System.IO;
using System.Net;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Reads the process's Service Fabric configuration store and environment, the same sources the native implementation reads.
/// </summary>
sealed class RuntimeMetricsSettingsSource : IMetricsSettingsSource
{
    readonly Lazy<NativeConfigStore> configStore = new(NativeConfigStore.FabricGetConfigStore);

    string IMetricsSettingsSource.ReadConfigValue(string sectionName, string keyName) =>
        configStore.Value.ReadUnencryptedString(sectionName, keyName);

    IEnumerable<string> IMetricsSettingsSource.GetConfigKeys(string sectionName) =>
        configStore.Value.GetAllKeys(sectionName) ?? (IEnumerable<string>)Array.Empty<string>();

    string IMetricsSettingsSource.GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    string IMetricsSettingsSource.GetHostName() => Dns.GetHostName();

    string IMetricsSettingsSource.GetFabricVersion()
    {
        string processPath = GetProcessPath();
        if (GenevaCollectorRules.IsLinux)
        {
            // Linux: contents of the ClusterVersion file next to the executable, or "" when it cannot be read.
            try
            {
                return File.ReadAllText(Path.Combine(Path.GetDirectoryName(processPath), "ClusterVersion"));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // Windows: file version of the process executable, or 0.0.0.0 when it cannot be read.
        try
        {
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(processPath);
            return $"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}.{version.FilePrivatePart}";
        }
        catch (Exception)
        {
            return "0.0.0.0";
        }
    }

    static string GetProcessPath()
    {
#if NET
        return Environment.ProcessPath;
#else
        using Process process = Process.GetCurrentProcess();
        return process.MainModule.FileName;
#endif
    }
}
