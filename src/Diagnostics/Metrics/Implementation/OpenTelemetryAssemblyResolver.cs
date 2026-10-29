// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

#if !NET
using System;
using System.Fabric.Common;
using System.IO;
using System.Reflection;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Loads assemblies of the OpenTelemetry backend that the app does not have, such as OpenTelemetry, the Geneva exporter,
/// System.Diagnostics.DiagnosticSource and Microsoft.Extensions, from the OpenTelemetry folder of the Service Fabric runtime.
/// </summary>
static class OpenTelemetryAssemblyResolver
{
    // Replaced by tests.
    internal static Func<string> GetFolder = static () => Path.Combine(FabricEnvironment.GetCodePath(), "OpenTelemetry");

    static ResolveEventHandler handler;

    /// <summary>
    /// Starts resolving assemblies from the folder when it exists. <see cref="SfMetricsPipeline"/> calls it under its lock.
    /// </summary>
    internal static void Register()
    {
        if (handler != null)
            return;

        string folder;
        try
        {
            folder = GetFolder();
        }
        catch (Exception)
        {
            // The code path can be read only where the Service Fabric runtime is installed.
            return;
        }

        if (Directory.Exists(folder))
        {
            handler = (_, args) => Resolve(folder, args.Name);
            AppDomain.CurrentDomain.AssemblyResolve += handler;
        }
    }

    /// <summary>
    /// Returns the assembly from its file in the folder, or null when the folder has no such file or only an older version.
    /// </summary>
    internal static Assembly Resolve(string folder, string assemblyName)
    {
        try
        {
            AssemblyName requested = new(assemblyName);
            string path = Path.Combine(folder, requested.Name + ".dll");
            if (!File.Exists(path) || AssemblyName.GetAssemblyName(path).Version < requested.Version)
                return null;

            return Assembly.LoadFrom(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Test hook: stops resolving assemblies from the folder.
    /// </summary>
    internal static void ResetForTesting()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= handler;
        handler = null;
    }
}
#endif
