// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Fabric.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Supplies the OpenTelemetry backend's assemblies to a .NET Framework app that ships no OpenTelemetry assembly, from the Service
/// Fabric code folder (Fabric.Code), where Service Fabric setup installs the .NET Framework builds of OpenTelemetry, the Geneva
/// exporter and their dependencies. An app that ships any OpenTelemetry assembly gets none of them, so Service Fabric's OpenTelemetry
/// never mixes with the app's: the app's own assemblies must then satisfy the backend, or it uses COM interop.
/// </summary>
/// <remarks>
/// The <see cref="AppDomain.AssemblyResolve"/> handler runs only for assemblies the app cannot load itself, and supplies only
/// Service Fabric's metrics dependencies: assemblies this assembly references, or an assembly loaded from the code folder references.
/// .NET Framework usually reports no requesting assembly, so the name decides; a request it does attribute to the app's own code is
/// declined. The folder's file must have the requested name and public key token and at least the requested version.
/// </remarks>
sealed class OpenTelemetryAssemblyResolver
{
    const string TraceType = SfMetricsPipeline.TraceType;
    const string OpenTelemetryName = "OpenTelemetry";
    const string AssemblyBindingNamespace = "urn:schemas-microsoft-com:asm.v1";

    static readonly object syncRoot = new();
    static bool decided;
    static ResolveEventHandler installedHandler;

    // Replaced by tests.
    internal static Func<string> ReadFabricCodePath = static () => FabricEnvironment.GetCodePath();
    internal static Func<IEnumerable<string>> ReadAppProbingDirectories = ReadAppDomainProbingDirectories;
    internal static Func<IEnumerable<Assembly>> ReadLoadedAssemblies = static () => AppDomain.CurrentDomain.GetAssemblies();
    internal static Action<ResolveEventHandler> Subscribe = static handler => AppDomain.CurrentDomain.AssemblyResolve += handler;

    readonly string folder;
    readonly Assembly serviceFabricMetrics;
    readonly Func<string, AssemblyName> readAssemblyName;
    readonly Func<string, Assembly> loadFrom;
    readonly HashSet<string> tracedEvents = new(StringComparer.OrdinalIgnoreCase);

    internal OpenTelemetryAssemblyResolver(
        string folder, Assembly serviceFabricMetrics, Func<string, AssemblyName> readAssemblyName, Func<string, Assembly> loadFrom)
    {
        this.folder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        this.serviceFabricMetrics = serviceFabricMetrics;
        this.readAssemblyName = readAssemblyName;
        this.loadFrom = loadFrom;
    }

    /// <summary>
    /// Decides once per AppDomain whether to supply Service Fabric's OpenTelemetry assemblies and, when it does, handles
    /// <see cref="AppDomain.AssemblyResolve"/>. Call before anything loads OpenTelemetry or System.Diagnostics.DiagnosticSource.
    /// Never throws.
    /// </summary>
    internal static void Install()
    {
        lock (syncRoot)
        {
            if (decided)
                return;

            decided = true;
            try
            {
                installedHandler = CreateHandler();
                if (installedHandler != null)
                    Subscribe(installedHandler);
            }
            catch (Exception e)
            {
                installedHandler = null;
                MetricsTrace.Warning(TraceType, $"Failed to decide whether to supply Service Fabric's OpenTelemetry assemblies; supplying none. Error: {e}");
            }
        }
    }

    /// <summary>
    /// Removes the handler and forgets the decision.
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (syncRoot)
        {
            if (installedHandler != null)
                AppDomain.CurrentDomain.AssemblyResolve -= installedHandler;

            installedHandler = null;
            decided = false;
        }
    }

    /// <summary>
    /// Returns the assembly Service Fabric supplies for the request, or null.
    /// </summary>
    internal Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string path = null;
        try
        {
            AssemblyName requested = new(args.Name);
            if (!string.IsNullOrEmpty(requested.CultureName))
                return null;

            // The event fires for every assembly the app fails to load, so most requests stop here.
            path = Path.Combine(folder, requested.Name + ".dll");
            if (!File.Exists(path) || !IsServiceFabricDependency(requested.Name, args.RequestingAssembly))
                return null;

            AssemblyName candidate = readAssemblyName(path);
            if (!IsCompatible(requested, candidate))
            {
                TraceOnce(
                    MetricsTraceLevel.Warning,
                    "declined " + path,
                    $"Service Fabric does not supply '{path}' ({candidate.FullName}) for {args.Name}, because the name, public key token or version does not match; the assembly fails to load.");
                return null;
            }

            Assembly assembly = loadFrom(path);
            TraceOnce(MetricsTraceLevel.Info, "supplied " + path, $"Service Fabric supplied '{path}' ({assembly.FullName}) for {args.Name}.");
            return assembly;
        }
        catch (Exception e)
        {
            TraceOnce(MetricsTraceLevel.Warning, "failed " + (path ?? args.Name), $"Service Fabric failed to supply {args.Name} from '{folder}'. Error: {e}");
            return null;
        }
    }

    static ResolveEventHandler CreateHandler()
    {
        string appOpenTelemetry = FindAppOpenTelemetry();
        if (appOpenTelemetry != null)
        {
            MetricsTrace.Info(TraceType, $"The app provides OpenTelemetry ({appOpenTelemetry}), so Service Fabric supplies none of its OpenTelemetry assemblies.");
            return null;
        }

        string codePath;
        try
        {
            codePath = ReadFabricCodePath();
        }
        catch (Exception e)
        {
            MetricsTrace.Info(
                TraceType,
                $"The app ships no OpenTelemetry assembly, but Service Fabric supplies none, because its code folder could not be read. Error: {e.Message}");
            return null;
        }

        if (string.IsNullOrEmpty(codePath) || !File.Exists(Path.Combine(codePath, OpenTelemetryName + ".dll")))
        {
            MetricsTrace.Info(
                TraceType,
                $"The app ships no OpenTelemetry assembly, but Service Fabric supplies none, because its code folder '{codePath}' has no {OpenTelemetryName}.dll.");
            return null;
        }

        OpenTelemetryAssemblyResolver resolver = new(codePath, typeof(OpenTelemetryAssemblyResolver).Assembly, AssemblyName.GetAssemblyName, Assembly.LoadFrom);
        MetricsTrace.Info(
            TraceType,
            $"The app ships no OpenTelemetry assembly, so Service Fabric supplies OpenTelemetry and the dependencies the app cannot load from '{resolver.folder}'.");
        return resolver.Resolve;
    }

    /// <summary>
    /// Returns where the app provides an OpenTelemetry assembly (OpenTelemetry or OpenTelemetry.*), already loaded or as a file in a
    /// folder the runtime probes for the app's assemblies, or null when it provides none.
    /// </summary>
    static string FindAppOpenTelemetry()
    {
        foreach (Assembly assembly in ReadLoadedAssemblies())
        {
            if (IsOpenTelemetry(assembly.GetName().Name))
                return $"loaded assembly {assembly.FullName}";
        }

        foreach (string directory in ReadAppProbingDirectories())
        {
            if (!Directory.Exists(directory))
                continue;

            // Matches names in code rather than with a search pattern, which is case-sensitive on Linux and, on .NET Framework, also
            // matches 8.3 short names.
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                if (string.Equals(Path.GetExtension(file), ".dll", StringComparison.OrdinalIgnoreCase) &&
                    IsOpenTelemetry(Path.GetFileNameWithoutExtension(file)))
                {
                    return $"'{file}'";
                }
            }
        }

        return null;
    }

    static bool IsOpenTelemetry(string assemblyName) =>
        string.Equals(assemblyName, OpenTelemetryName, StringComparison.OrdinalIgnoreCase) ||
        (assemblyName?.StartsWith(OpenTelemetryName + ".", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// Returns the folders the runtime probes for the app's assemblies in this AppDomain.
    /// </summary>
    static IEnumerable<string> ReadAppDomainProbingDirectories()
    {
        AppDomain domain = AppDomain.CurrentDomain;
#if NETFRAMEWORK
        string configurationFile = domain.SetupInformation.ConfigurationFile;
#else
        string configurationFile = null;
#endif
        return GetProbingDirectories(domain.BaseDirectory, domain.RelativeSearchPath, configurationFile);
    }

    /// <summary>
    /// Returns the application base, then the folders of the AppDomain's private bin path and of the configuration file's private
    /// path (runtime/assemblyBinding/probing), both relative to the application base.
    /// </summary>
    internal static IEnumerable<string> GetProbingDirectories(string baseDirectory, string relativeSearchPath, string configurationFile) =>
        new[] { baseDirectory }
            .Concat(SplitPrivatePath(baseDirectory, relativeSearchPath))
            .Concat(SplitPrivatePath(baseDirectory, ReadConfigurationPrivatePath(configurationFile)));

    static IEnumerable<string> SplitPrivatePath(string baseDirectory, string privatePath) =>
        (privatePath ?? string.Empty)
            .Split(';')
            .Select(static path => path.Trim())
            .Where(static path => path.Length > 0)
            .Select(path => Path.Combine(baseDirectory, path));

    /// <summary>
    /// Returns the semicolon-separated private paths of the configuration file's probing elements, or null when it has none.
    /// Throws when the file exists but cannot be read, so that an app whose folders are unknown is not supplied.
    /// </summary>
    internal static string ReadConfigurationPrivatePath(string configurationFile)
    {
        if (string.IsNullOrEmpty(configurationFile) || !File.Exists(configurationFile))
            return null;

        XNamespace binding = AssemblyBindingNamespace;
        IEnumerable<string> privatePaths = XDocument.Load(configurationFile).Root?
            .Elements("runtime")
            .Elements(binding + "assemblyBinding")
            .Elements(binding + "probing")
            .Select(static probing => (string)probing.Attribute("privatePath"))
            .Where(static path => !string.IsNullOrEmpty(path));

        return privatePaths == null ? null : string.Join(";", privatePaths);
    }

    /// <summary>
    /// Whether Service Fabric's metrics code may need the assembly: this assembly or an assembly loaded from the code folder references
    /// it, and the request does not come from the app's own code.
    /// </summary>
    bool IsServiceFabricDependency(string name, Assembly requesting)
    {
        if (requesting != null && requesting != serviceFabricMetrics && !IsInGlobalAssemblyCache(requesting) && !IsFromFolder(requesting))
            return false;

        if (References(serviceFabricMetrics, name))
            return true;

        foreach (Assembly assembly in ReadLoadedAssemblies())
        {
            if (IsFromFolder(assembly) && References(assembly, name))
                return true;
        }

        return false;
    }

    static bool IsInGlobalAssemblyCache(Assembly assembly) =>
#if NETFRAMEWORK
        assembly.GlobalAssemblyCache;
#else
        false;
#endif

    bool IsFromFolder(Assembly assembly)
    {
        if (assembly.IsDynamic)
            return false;

        string location = assembly.Location;
        return !string.IsNullOrEmpty(location) && string.Equals(Path.GetDirectoryName(location), folder, StringComparison.OrdinalIgnoreCase);
    }

    static bool References(Assembly assembly, string name)
    {
        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
        {
            if (string.Equals(reference.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static bool IsCompatible(AssemblyName requested, AssemblyName candidate) =>
        string.Equals(candidate.Name, requested.Name, StringComparison.OrdinalIgnoreCase) &&
        TokensEqual(requested.GetPublicKeyToken(), candidate.GetPublicKeyToken()) &&
        (requested.Version == null || (candidate.Version != null && candidate.Version >= requested.Version));

    // Compares by hand: LINQ's SequenceEqual can bind to System.Memory, which this class may itself be asked to supply.
    static bool TokensEqual(byte[] requested, byte[] candidate)
    {
        int length = requested?.Length ?? 0;
        if ((candidate?.Length ?? 0) != length)
            return false;

        for (int i = 0; i < length; i++)
        {
            if (requested[i] != candidate[i])
                return false;
        }

        return true;
    }

    void TraceOnce(MetricsTraceLevel level, string key, string message)
    {
        lock (tracedEvents)
        {
            if (!tracedEvents.Add(key))
                return;
        }

        if (level == MetricsTraceLevel.Info)
            MetricsTrace.Info(TraceType, message);
        else
            MetricsTrace.Warning(TraceType, message);
    }
}
