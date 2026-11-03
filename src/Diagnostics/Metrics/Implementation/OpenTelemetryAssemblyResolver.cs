// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

#if !NET
using System;
using System.Fabric.Common;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Resolves assemblies of the OpenTelemetry backend that the app fails to load, such as OpenTelemetry, the Geneva exporter,
/// System.Diagnostics.DiagnosticSource and Microsoft.Extensions, when the OpenTelemetry folder of the Service Fabric runtime
/// has them: uses the app's own copy when it is newer than the requested version, and the folder's copy otherwise.
/// </summary>
static class OpenTelemetryAssemblyResolver
{
    // Replaced by tests.
    internal static Func<string> GetFolder = static () => Path.Combine(FabricEnvironment.GetCodePath(), "OpenTelemetry");

    static ResolveEventHandler handler;

    // Stops the recursion when loading the app's copy fails and raises AssemblyResolve again.
    [ThreadStatic] static bool loadingAppCopy;

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
    /// Returns the app's copy of an assembly the folder has when the app's copy is newer than the requested version, otherwise
    /// the assembly from its file in the folder, or null when the folder has no such file or only an older version.
    /// </summary>
    internal static Assembly Resolve(string folder, string assemblyName)
    {
        try
        {
            AssemblyName requested = new(assemblyName);
            string path = Path.Combine(folder, requested.Name + ".dll");
            if (!File.Exists(path))
                return null;

            // The app's own code uses its newer copy; a second copy from the folder would share no types or state with it.
            AssemblyName appCopy = loadingAppCopy ? null : FindNewerAppCopy(requested);
            if (appCopy != null)
                return LoadAppCopy(appCopy);

            if (AssemblyName.GetAssemblyName(path).Version < requested.Version)
                return null;

            return Assembly.LoadFrom(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    static AssemblyName FindNewerAppCopy(AssemblyName requested)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, requested.Name + ".dll");
        if (!File.Exists(path))
            return null;

        AssemblyName appCopy = AssemblyName.GetAssemblyName(path);
        return appCopy.Version > requested.Version && HasSamePublicKeyToken(appCopy, requested) ? appCopy : null;
    }

    // Loads by name, like the app's own references, so the app's code and this library share it in either load order.
    static Assembly LoadAppCopy(AssemblyName appCopy)
    {
        loadingAppCopy = true;
        try
        {
            return Assembly.Load(appCopy.FullName);
        }
        finally
        {
            loadingAppCopy = false;
        }
    }

    static bool HasSamePublicKeyToken(AssemblyName a, AssemblyName b) =>
        (a.GetPublicKeyToken() ?? []).SequenceEqual(b.GetPublicKeyToken() ?? []);

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
