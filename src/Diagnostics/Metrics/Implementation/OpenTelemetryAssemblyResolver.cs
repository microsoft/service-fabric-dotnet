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
/// Resolves the OpenTelemetry backend's assemblies in a .NET Framework app. An app that ships no OpenTelemetry assembly is supplied
/// from the Service Fabric code folder (Fabric.Code), where Service Fabric setup installs the .NET Framework builds of OpenTelemetry,
/// the Geneva exporter and their dependencies. An app that ships OpenTelemetry keeps its own assemblies: a newer app copy of an
/// assembly the backend references stands in for the referenced version, as the binding redirect the app's build writes for the
/// references it knows of would, and an assembly the app lacks entirely is supplied from the code folder's fill-in folder, which
/// holds the oldest versions the backend supports. An older app copy is never replaced, so the backend uses COM interop instead.
/// </summary>
/// <remarks>
/// The <see cref="AppDomain.AssemblyResolve"/> handler runs only for assemblies the app cannot load itself, and resolves only
/// Service Fabric's metrics dependencies. For an app that ships no OpenTelemetry, those are assemblies this assembly references, or
/// an assembly loaded from the code folder references; for an app that ships OpenTelemetry, only the exact references of this
/// assembly. .NET Framework usually reports no requesting assembly, so the name decides; a request it does attribute to the app's
/// own code is declined. A supplied file must have the requested name and public key token and at least the requested version; an
/// app copy that stands in must also have the requested major version, except System.Diagnostics.DiagnosticSource and
/// Microsoft.Extensions assemblies, whose major version follows each .NET release.
/// </remarks>
sealed class OpenTelemetryAssemblyResolver
{
    const string TraceType = SfMetricsPipeline.TraceType;
    const string OpenTelemetryName = "OpenTelemetry";
    const string AssemblyBindingNamespace = "urn:schemas-microsoft-com:asm.v1";

    // In the code folder: the oldest versions the backend supports of assemblies that an app that ships OpenTelemetry may lack, such
    // as the Geneva exporter this assembly is compiled against, so that they work with any newer OpenTelemetry 1.x the app ships.
    internal const string FillInFolderName = "OpenTelemetryFillIn";

    static readonly object syncRoot = new();
    static bool decided;
    static ResolveEventHandler installedHandler;

    // Replaced by tests.
    internal static Func<string> ReadFabricCodePath = static () => FabricEnvironment.GetCodePath();
    internal static Func<IEnumerable<string>> ReadAppProbingDirectories = ReadAppDomainProbingDirectories;
    internal static Func<IEnumerable<Assembly>> ReadLoadedAssemblies = static () => AppDomain.CurrentDomain.GetAssemblies();
    internal static Action<ResolveEventHandler> Subscribe = static handler => AppDomain.CurrentDomain.AssemblyResolve += handler;
    internal static Action<ResolveEventHandler> Unsubscribe = static handler => AppDomain.CurrentDomain.AssemblyResolve -= handler;

    readonly string folder;
    readonly Assembly serviceFabricMetrics;
    readonly Func<string, AssemblyName> readAssemblyName;
    readonly Func<string, Assembly> loadFrom;
    readonly IReadOnlyList<string> appProbingDirectories;
    readonly Func<AssemblyName, Assembly> load;
    readonly HashSet<string> tracedEvents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a resolver that supplies an app that ships no OpenTelemetry assembly from the folder.
    /// </summary>
    internal OpenTelemetryAssemblyResolver(
        string folder, Assembly serviceFabricMetrics, Func<string, AssemblyName> readAssemblyName, Func<string, Assembly> loadFrom)
        : this(folder, serviceFabricMetrics, readAssemblyName, loadFrom, appProbingDirectories: null, load: null)
    {
    }

    /// <summary>
    /// Creates a resolver for an app that ships OpenTelemetry, whose assemblies are in <paramref name="appProbingDirectories"/>, when
    /// they are not null: newer app copies stand in, loaded with <paramref name="load"/>, and assemblies the app lacks come from the
    /// folder, unless it is null.
    /// </summary>
    internal OpenTelemetryAssemblyResolver(
        string folder,
        Assembly serviceFabricMetrics,
        Func<string, AssemblyName> readAssemblyName,
        Func<string, Assembly> loadFrom,
        IReadOnlyList<string> appProbingDirectories,
        Func<AssemblyName, Assembly> load)
    {
        this.folder = string.IsNullOrEmpty(folder) ? null : Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        this.serviceFabricMetrics = serviceFabricMetrics;
        this.readAssemblyName = readAssemblyName;
        this.loadFrom = loadFrom;
        this.appProbingDirectories = appProbingDirectories;
        this.load = load;
    }

    /// <summary>
    /// Decides once per AppDomain how to resolve the OpenTelemetry backend's assemblies and, when there is anything to resolve,
    /// handles <see cref="AppDomain.AssemblyResolve"/>. Call before anything loads OpenTelemetry or System.Diagnostics.DiagnosticSource.
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
    /// Stops handling <see cref="AppDomain.AssemblyResolve"/>, so that an app whose backend did not start and falls back to COM
    /// interop, which needs none of these assemblies, keeps no Service Fabric handler. Assemblies already resolved stay loaded, and
    /// the handler is not installed again. Never throws.
    /// </summary>
    internal static void Uninstall()
    {
        lock (syncRoot)
        {
            ResolveEventHandler handler = installedHandler;
            installedHandler = null;
            if (handler == null)
                return;

            try
            {
                Unsubscribe(handler);
                MetricsTrace.Info(TraceType, "The OpenTelemetry backend did not start, so Service Fabric stopped resolving OpenTelemetry assemblies for the app.");
            }
            catch (Exception e)
            {
                MetricsTrace.Warning(TraceType, $"Failed to stop resolving OpenTelemetry assemblies for the app. Error: {e}");
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
    /// Returns the assembly that resolves the request for Service Fabric's metrics, or null.
    /// </summary>
    internal Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string path = null;
        try
        {
            AssemblyName requested = new(args.Name);
            if (!string.IsNullOrEmpty(requested.CultureName))
                return null;

            if (appProbingDirectories != null)
                return ResolveForAppOpenTelemetry(requested, args, ref path);

            // The event fires for every assembly the app fails to load, so most requests stop here.
            path = Path.Combine(folder, requested.Name + ".dll");
            if (!File.Exists(path) || !IsServiceFabricDependency(requested.Name, args.RequestingAssembly))
                return null;

            return SupplyFromFolder(requested, args.Name, path);
        }
        catch (Exception e)
        {
            string message = appProbingDirectories == null
                ? $"Service Fabric failed to supply {args.Name} from '{folder}'. Error: {e}"
                : $"Service Fabric failed to resolve {args.Name} from the app's assemblies or '{folder}'. Error: {e}";
            TraceOnce(MetricsTraceLevel.Warning, "failed " + (path ?? args.Name), message);
            return null;
        }
    }

    Assembly SupplyFromFolder(AssemblyName requested, string request, string path)
    {
        AssemblyName candidate = readAssemblyName(path);
        if (!IsCompatible(requested, candidate))
        {
            TraceOnce(
                MetricsTraceLevel.Warning,
                "declined " + path,
                $"Service Fabric does not supply '{path}' ({candidate.FullName}) for {request}, because the name, public key token or version does not match; the assembly fails to load.");
            return null;
        }

        Assembly assembly = loadFrom(path);
        TraceOnce(MetricsTraceLevel.Info, "supplied " + path, $"Service Fabric supplied '{path}' ({assembly.FullName}) for {request}.");
        return assembly;
    }

    /// <summary>
    /// Resolves an exact reference of this assembly in an app that ships OpenTelemetry, which fails to load because the app has
    /// another version of the assembly or none: the app's copy stands in when it is newer, with the same public key token and a major
    /// version it may have, as a binding redirect would; the folder supplies the assembly only when the app has no copy at all.
    /// </summary>
    Assembly ResolveForAppOpenTelemetry(AssemblyName requested, ResolveEventArgs args, ref string path)
    {
        if (!IsServiceFabricReference(requested) || !IsServiceFabricOrUnknown(args.RequestingAssembly))
            return null;

        AssemblyName appCopy = FindAppCopy(requested.Name, out Assembly loadedAppCopy);
        if (appCopy != null)
        {
            // A loaded app copy of the requested version is the requested assembly. A file of that version is declined: loading it
            // would fail again for whatever reason the runtime just failed to load it.
            if (!CanStandIn(appCopy, requested, allowRequestedVersion: loadedAppCopy != null))
            {
                TraceOnce(
                    MetricsTraceLevel.Warning,
                    "declined app " + requested.Name,
                    $"The app's {appCopy.FullName} does not stand in for {args.Name}, which Service Fabric's metrics reference, because it is not a newer version with the same public key token and a supported major version; the assembly fails to load.");
                return null;
            }

            Assembly assembly = loadedAppCopy ?? load(appCopy);
            TraceOnce(
                MetricsTraceLevel.Info,
                "app " + requested.Name,
                $"The app's {assembly.FullName} stands in for {args.Name}, which Service Fabric's metrics reference, as a binding redirect would.");
            return assembly;
        }

        if (folder == null)
            return null;

        path = Path.Combine(folder, requested.Name + ".dll");
        return File.Exists(path) ? SupplyFromFolder(requested, args.Name, path) : null;
    }

    static ResolveEventHandler CreateHandler()
    {
        IReadOnlyList<string> probingDirectories = ReadAppProbingDirectories().ToArray();
        string appOpenTelemetry = FindAppOpenTelemetry(probingDirectories);
        if (appOpenTelemetry != null)
            return CreateHandlerForAppOpenTelemetry(appOpenTelemetry, probingDirectories);

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

    static ResolveEventHandler CreateHandlerForAppOpenTelemetry(string appOpenTelemetry, IReadOnlyList<string> probingDirectories)
    {
        string fillInFolder = null;
        try
        {
            string codePath = ReadFabricCodePath();
            if (!string.IsNullOrEmpty(codePath))
                fillInFolder = Path.Combine(codePath, FillInFolderName);
        }
        catch (Exception e)
        {
            MetricsTrace.Info(TraceType, $"Service Fabric's code folder could not be read. Error: {e.Message}");
        }

        OpenTelemetryAssemblyResolver resolver = new(
            fillInFolder, typeof(OpenTelemetryAssemblyResolver).Assembly, AssemblyName.GetAssemblyName, Assembly.LoadFrom, probingDirectories, Assembly.Load);
        string supply = resolver.folder == null
            ? "none of the assemblies the app lacks are supplied, because Service Fabric's code folder is unknown"
            : $"assemblies the app lacks are supplied from '{resolver.folder}'";
        MetricsTrace.Info(
            TraceType,
            $"The app provides OpenTelemetry ({appOpenTelemetry}), so Service Fabric's metrics use the app's OpenTelemetry assemblies: a newer app copy of an assembly they reference stands in for it, and {supply}.");
        return resolver.Resolve;
    }

    /// <summary>
    /// Returns where the app provides an OpenTelemetry assembly (OpenTelemetry or OpenTelemetry.*), already loaded or as a file in one
    /// of the folders the runtime probes for the app's assemblies, or null when it provides none.
    /// </summary>
    static string FindAppOpenTelemetry(IReadOnlyList<string> probingDirectories)
    {
        foreach (Assembly assembly in ReadLoadedAssemblies())
        {
            if (IsOpenTelemetry(assembly.GetName().Name))
                return $"loaded assembly {assembly.FullName}";
        }

        foreach (string directory in probingDirectories)
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
        if (!IsServiceFabricOrUnknown(requesting))
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

    /// <summary>
    /// Whether the requesting assembly is unknown, as .NET Framework usually reports, or Service Fabric's: this assembly, one in the
    /// global assembly cache or one loaded from the folder.
    /// </summary>
    bool IsServiceFabricOrUnknown(Assembly requesting) =>
        requesting == null || requesting == serviceFabricMetrics || IsInGlobalAssemblyCache(requesting) || IsFromFolder(requesting);

    /// <summary>
    /// Whether this assembly references exactly the requested name, version and public key token.
    /// </summary>
    bool IsServiceFabricReference(AssemblyName requested)
    {
        foreach (AssemblyName reference in serviceFabricMetrics.GetReferencedAssemblies())
        {
            if (string.Equals(reference.Name, requested.Name, StringComparison.OrdinalIgnoreCase) &&
                reference.Version != null &&
                reference.Version == requested.Version &&
                TokensEqual(reference.GetPublicKeyToken(), requested.GetPublicKeyToken()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the name of the app's copy of the assembly, the newest loaded one or else the first file in the folders the runtime
    /// probes for the app's assemblies, or null when the app has none. A loaded copy is also returned in <paramref name="loaded"/>.
    /// </summary>
    AssemblyName FindAppCopy(string name, out Assembly loaded)
    {
        loaded = null;
        AssemblyName loadedName = null;
        foreach (Assembly assembly in ReadLoadedAssemblies())
        {
            if (assembly == serviceFabricMetrics || assembly.IsDynamic || IsFromFolder(assembly))
                continue;

            AssemblyName assemblyName = assembly.GetName();
            if (string.Equals(assemblyName.Name, name, StringComparison.OrdinalIgnoreCase) &&
                assemblyName.Version != null &&
                (loadedName == null || assemblyName.Version > loadedName.Version))
            {
                loaded = assembly;
                loadedName = assemblyName;
            }
        }

        if (loadedName != null)
            return loadedName;

        foreach (string directory in appProbingDirectories)
        {
            string path = Path.Combine(directory, name + ".dll");
            if (File.Exists(path))
                return readAssemblyName(path);
        }

        return null;
    }

    /// <summary>
    /// Whether the app's copy may stand in for the requested assembly: the same name and public key token, and a newer version, or
    /// the requested version itself when allowed. The major version must match too, unless the assembly may have a newer one.
    /// </summary>
    static bool CanStandIn(AssemblyName appCopy, AssemblyName requested, bool allowRequestedVersion) =>
        string.Equals(appCopy.Name, requested.Name, StringComparison.OrdinalIgnoreCase) &&
        TokensEqual(requested.GetPublicKeyToken(), appCopy.GetPublicKeyToken()) &&
        appCopy.Version != null &&
        requested.Version != null &&
        (appCopy.Version.Major == requested.Version.Major || AllowsNewerMajorVersion(requested.Name)) &&
        (appCopy.Version > requested.Version || (allowRequestedVersion && appCopy.Version == requested.Version));

    /// <summary>
    /// Whether a newer major version of the assembly may stand in, as for System.Diagnostics.DiagnosticSource and Microsoft.Extensions
    /// assemblies: their major version follows each .NET release while they stay compatible, and the binding redirects that MSBuild
    /// generates for them cross major versions too. OpenTelemetry and the Geneva exporter keep their major version, which
    /// <see cref="SfOpenTelemetrySdk.EnsureSupportedVersion"/> also checks.
    /// </summary>
    static bool AllowsNewerMajorVersion(string assemblyName) =>
        string.Equals(assemblyName, "System.Diagnostics.DiagnosticSource", StringComparison.OrdinalIgnoreCase) ||
        assemblyName.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase);

    static bool IsInGlobalAssemblyCache(Assembly assembly) =>
#if NETFRAMEWORK
        assembly.GlobalAssemblyCache;
#else
        false;
#endif

    bool IsFromFolder(Assembly assembly)
    {
        if (folder == null || assembly.IsDynamic)
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
