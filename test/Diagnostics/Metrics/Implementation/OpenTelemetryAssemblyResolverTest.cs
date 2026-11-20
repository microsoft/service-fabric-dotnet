// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Tests supplying Service Fabric's OpenTelemetry assemblies to a .NET Framework app that ships none. The AppDomain's AssemblyResolve
/// event is never handled: tests capture the handler and resolve with fake loads.
/// </summary>
public abstract class OpenTelemetryAssemblyResolverTest : IDisposable
{
    const string TraceType = "OpenTelemetryMetricsFactory";
    const string OpenTelemetryRequest = "OpenTelemetry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c";

    readonly MetricsTestScope scope = new();
    readonly Func<IEnumerable<string>> readAppDomainProbingDirectories = OpenTelemetryAssemblyResolver.ReadAppProbingDirectories;
    readonly string root = Path.Combine(Path.GetTempPath(), "SfOpenTelemetryResolverTest" + Guid.NewGuid().ToString("N"));
    readonly string appDirectory;
    readonly string codeFolder;
    readonly List<ResolveEventHandler> handlers = [];

    protected OpenTelemetryAssemblyResolverTest()
    {
        appDirectory = Path.GetFullPath(Directory.CreateDirectory(Path.Combine(root, "app")).FullName);
        codeFolder = Path.GetFullPath(Directory.CreateDirectory(Path.Combine(root, "Fabric.Code")).FullName);
        OpenTelemetryAssemblyResolver.ReadAppProbingDirectories = () => [appDirectory];
        OpenTelemetryAssemblyResolver.ReadLoadedAssemblies = () => [typeof(object).Assembly, typeof(OpenTelemetryAssemblyResolver).Assembly];
        OpenTelemetryAssemblyResolver.ReadFabricCodePath = () => codeFolder;
        OpenTelemetryAssemblyResolver.Subscribe = handlers.Add;
    }

    void IDisposable.Dispose()
    {
        ((IDisposable)scope).Dispose();
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    TraceRecord AssertSingleTrace(MetricsTraceLevel level, string messageStart) =>
        Assert.Single(scope.TracesOfType(TraceType), t => t.Level == level && t.Message.StartsWith(messageStart, StringComparison.Ordinal));

    static void CreateFile(string directory, string fileName) => File.WriteAllBytes(Path.Combine(directory, fileName), []);

    static string CopyAssembly(Assembly assembly, string directory)
    {
        string path = Path.Combine(directory, Path.GetFileName(assembly.Location));
        File.Copy(assembly.Location, path);
        return path;
    }

    protected sealed class FakeAssembly(string location, bool isDynamic = false, AssemblyName[] references = null, string fullName = null) : Assembly
    {
        public override string Location => location;

        public override bool IsDynamic => isDynamic;

#if NETFRAMEWORK
        public override bool GlobalAssemblyCache => false;
#endif

        public override string FullName => fullName ?? "Fake, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

        public override AssemblyName GetName() => new(FullName);

        public override AssemblyName[] GetReferencedAssemblies() => references ?? [];
    }

    public sealed class Install : OpenTelemetryAssemblyResolverTest
    {
        [Fact]
        public void HandlesAssemblyResolveWhenAppShipsNoOpenTelemetryAssembly()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            CreateFile(appDirectory, "System.Diagnostics.DiagnosticSource.dll");
            CreateFile(appDirectory, "Microsoft.Extensions.Logging.dll");
            CreateFile(appDirectory, "OpenTelemetryLike.dll");
            CreateFile(appDirectory, "OpenTelemetry.dll.config");

            OpenTelemetryAssemblyResolver.Install();

            _ = Assert.Single(handlers);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, $"The app ships no OpenTelemetry assembly, so Service Fabric supplies OpenTelemetry and the dependencies the app cannot load from '{codeFolder}'.");
        }

        [Theory]
        [InlineData("OpenTelemetry.dll")]
        [InlineData("OpenTelemetry.Api.dll")]
        [InlineData("opentelemetry.exporter.geneva.dll")]
        [InlineData("OPENTELEMETRY.API.DLL")]
        [InlineData("OpenTelemetry.Instrumentation.Http.dll")]
        public void ResolvesWithAppAssembliesWhenAppShipsAnyOpenTelemetryAssembly(string fileName)
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            CreateFile(appDirectory, fileName);

            OpenTelemetryAssemblyResolver.Install();

            _ = Assert.Single(handlers);
            TraceRecord trace = AssertSingleTrace(MetricsTraceLevel.Info, "The app provides OpenTelemetry (");
            Assert.Contains(Path.Combine(appDirectory, fileName), trace.Message, StringComparison.Ordinal);
            Assert.EndsWith(
                $"a newer app copy of an assembly they reference stands in for it, and assemblies the app lacks are supplied from '{Path.Combine(codeFolder, OpenTelemetryAssemblyResolver.FillInFolderName)}'.",
                trace.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void ResolvesWithAppAssembliesWhenAnyProbingDirectoryHasOpenTelemetryAssembly()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            string privateDirectory = Directory.CreateDirectory(Path.Combine(appDirectory, "lib")).FullName;
            CreateFile(privateDirectory, "OpenTelemetry.Api.dll");
            OpenTelemetryAssemblyResolver.ReadAppProbingDirectories = () => [appDirectory, Path.Combine(root, "missing"), privateDirectory];

            OpenTelemetryAssemblyResolver.Install();

            _ = Assert.Single(handlers);
            TraceRecord trace = AssertSingleTrace(MetricsTraceLevel.Info, "The app provides OpenTelemetry (");
            Assert.Contains(Path.Combine(privateDirectory, "OpenTelemetry.Api.dll"), trace.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolvesWithAppAssembliesWhenOpenTelemetryAssemblyIsLoaded()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            OpenTelemetryAssemblyResolver.ReadLoadedAssemblies = () => [typeof(object).Assembly, typeof(Baggage).Assembly];

            OpenTelemetryAssemblyResolver.Install();

            _ = Assert.Single(handlers);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, $"The app provides OpenTelemetry (loaded assembly {typeof(Baggage).Assembly.FullName}),");
        }

        [Fact]
        public void ResolvesWithAppAssembliesButSuppliesNoneWhenAppShipsOpenTelemetryAndCodeFolderCannotBeRead()
        {
            CreateFile(appDirectory, "OpenTelemetry.dll");
            OpenTelemetryAssemblyResolver.ReadFabricCodePath = () => throw new COMException("FabricCodePathNotFound");

            OpenTelemetryAssemblyResolver.Install();

            _ = Assert.Single(handlers);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, "Service Fabric's code folder could not be read.");
            TraceRecord trace = AssertSingleTrace(MetricsTraceLevel.Info, "The app provides OpenTelemetry (");
            Assert.EndsWith(
                "and none of the assemblies the app lacks are supplied, because Service Fabric's code folder is unknown.", trace.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SuppliesNothingWhenCodeFolderCannotBeRead()
        {
            OpenTelemetryAssemblyResolver.ReadFabricCodePath = () => throw new COMException("FabricCodePathNotFound");

            OpenTelemetryAssemblyResolver.Install();

            Assert.Empty(handlers);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, "The app ships no OpenTelemetry assembly, but Service Fabric supplies none, because its code folder could not be read.");
        }

        [Fact]
        public void SuppliesNothingWhenCodeFolderHasNoOpenTelemetry()
        {
            CreateFile(codeFolder, "OpenTelemetry.Api.dll");

            OpenTelemetryAssemblyResolver.Install();

            Assert.Empty(handlers);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, $"The app ships no OpenTelemetry assembly, but Service Fabric supplies none, because its code folder '{codeFolder}' has no OpenTelemetry.dll.");
        }

        [Fact]
        public void SuppliesNothingAndTracesWarningWhenDecidingThrows()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            OpenTelemetryAssemblyResolver.ReadAppProbingDirectories = () => throw new XmlException("Malformed configuration file.");

            OpenTelemetryAssemblyResolver.Install();

            Assert.Empty(handlers);
            _ = AssertSingleTrace(MetricsTraceLevel.Warning, "Failed to decide whether to supply Service Fabric's OpenTelemetry assemblies; supplying none.");
        }

        [Fact]
        public void DecidesOnce()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");

            OpenTelemetryAssemblyResolver.Install();
            OpenTelemetryAssemblyResolver.Install();

            _ = Assert.Single(handlers);
        }

        [Fact]
        public void UninstallStopsHandlingAssemblyResolveForGood()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            List<ResolveEventHandler> removed = [];
            OpenTelemetryAssemblyResolver.Unsubscribe = removed.Add;
            OpenTelemetryAssemblyResolver.Install();

            OpenTelemetryAssemblyResolver.Uninstall();
            OpenTelemetryAssemblyResolver.Install();
            OpenTelemetryAssemblyResolver.Uninstall();

            Assert.Equal(handlers, removed);
            _ = AssertSingleTrace(
                MetricsTraceLevel.Info, "The OpenTelemetry backend did not start, so Service Fabric stopped resolving OpenTelemetry assemblies for the app.");
        }

        [Fact]
        public void UninstallDoesNothingWithoutHandler()
        {
            List<ResolveEventHandler> removed = [];
            OpenTelemetryAssemblyResolver.Unsubscribe = removed.Add;

            OpenTelemetryAssemblyResolver.Uninstall();
            OpenTelemetryAssemblyResolver.Install();
            OpenTelemetryAssemblyResolver.Uninstall();

            Assert.Empty(handlers);
            Assert.Empty(removed);
            Assert.DoesNotContain(scope.TracesOfType(TraceType), t => t.Message.StartsWith("The OpenTelemetry backend did not start", StringComparison.Ordinal));
        }

        [Fact]
        public void UninstallTracesWarningWhenUnsubscribingThrows()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            OpenTelemetryAssemblyResolver.Unsubscribe = _ => throw new InvalidOperationException("Unsubscribing failed.");
            OpenTelemetryAssemblyResolver.Install();

            OpenTelemetryAssemblyResolver.Uninstall();

            _ = AssertSingleTrace(MetricsTraceLevel.Warning, "Failed to stop resolving OpenTelemetry assemblies for the app.");
        }

        [Fact]
        public void PipelineUninstallsResolverOnlyOnDotNetFramework()
        {
#if NETFRAMEWORK
            Assert.Equal(typeof(OpenTelemetryAssemblyResolver), scope.OriginalWithdrawOpenTelemetry.Method.DeclaringType);
            Assert.Equal(nameof(OpenTelemetryAssemblyResolver.Uninstall), scope.OriginalWithdrawOpenTelemetry.Method.Name);
#else
            Assert.NotEqual(typeof(OpenTelemetryAssemblyResolver), scope.OriginalWithdrawOpenTelemetry.Method.DeclaringType);
#endif
        }

        [Fact]
        public void PipelineInstallsResolverOnlyOnDotNetFramework()
        {
#if NETFRAMEWORK
            Assert.Equal(typeof(OpenTelemetryAssemblyResolver), scope.OriginalSupplyOpenTelemetry.Method.DeclaringType);
            Assert.Equal(nameof(OpenTelemetryAssemblyResolver.Install), scope.OriginalSupplyOpenTelemetry.Method.Name);
#else
            Assert.NotEqual(typeof(OpenTelemetryAssemblyResolver), scope.OriginalSupplyOpenTelemetry.Method.DeclaringType);
#endif
        }

        [Fact]
        public void ProbesApplicationBaseFirst()
        {
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, readAppDomainProbingDirectories().First());
        }
    }

    public sealed class Resolve : OpenTelemetryAssemblyResolverTest
    {
        readonly List<string> loads = [];
        readonly Assembly loaded = typeof(object).Assembly;
        readonly OpenTelemetryAssemblyResolver resolver;

        public Resolve() => resolver = CreateResolver(codeFolder);

        static Assembly ServiceFabricMetrics => typeof(OpenTelemetryAssemblyResolver).Assembly;

        OpenTelemetryAssemblyResolver CreateResolver(string folder) => new(folder, ServiceFabricMetrics, AssemblyName.GetAssemblyName, path =>
        {
            loads.Add(path);
            return loaded;
        });

        [Fact]
        public void SuppliesAssemblyServiceFabricReferencesWhenRequesterIsUnknown()
        {
            string path = CopyAssembly(typeof(Sdk).Assembly, codeFolder);

            Assembly assembly = resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest));

            Assert.Same(loaded, assembly);
            Assert.Equal([path], loads);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, $"Service Fabric supplied '{path}' ({loaded.FullName}) for {OpenTelemetryRequest}.");
        }

        [Fact]
        public void SuppliesAssemblyToServiceFabricMetrics()
        {
            string path = CopyAssembly(typeof(Sdk).Assembly, codeFolder);

            Assert.Same(loaded, resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest, ServiceFabricMetrics)));

            Assert.Equal([path], loads);
        }

        [Fact]
        public void SuppliesLaterVersionThanRequested()
        {
            string path = CopyAssembly(typeof(GenevaMetricExporter).Assembly, codeFolder);
            AssemblyName request = AssemblyName.GetAssemblyName(path);
            request.Version = new Version(request.Version.Major, request.Version.Minor - 1, 0, 0);

            Assembly assembly = resolver.Resolve(null, new ResolveEventArgs(request.FullName));

            Assert.Same(loaded, assembly);
            Assert.Equal([path], loads);
        }

        [Fact]
        public void SuppliesDependencyOfAssemblyLoadedFromCodeFolder()
        {
            string path = CopyAssembly(typeof(Mock).Assembly, codeFolder);
            string request = AssemblyName.GetAssemblyName(path).FullName;
            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(request)));
            OpenTelemetryAssemblyResolver.ReadLoadedAssemblies = () =>
                [typeof(object).Assembly, new FakeAssembly(Path.Combine(codeFolder, "OpenTelemetry.dll"), references: [new AssemblyName(request)])];

            Assembly assembly = resolver.Resolve(null, new ResolveEventArgs(request));

            Assert.Same(loaded, assembly);
            Assert.Equal([path], loads);
        }

        [Fact]
        public void DeclinesAssemblyServiceFabricDoesNotReference()
        {
            string path = CopyAssembly(typeof(Mock).Assembly, codeFolder);
            OpenTelemetryAssemblyResolver.ReadLoadedAssemblies = () =>
                [typeof(Mock).Assembly, new FakeAssembly(Path.Combine(appDirectory, "App.dll"), references: [AssemblyName.GetAssemblyName(path)])];

            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(AssemblyName.GetAssemblyName(path).FullName)));

            Assert.Empty(loads);
            Assert.Empty(scope.TracesOfType(TraceType));
        }

        [Theory]
        [InlineData("OpenTelemetry, Version=1.0.0.1, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c")]
        [InlineData("OpenTelemetry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef")]
        [InlineData("OpenTelemetry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
        public void DeclinesWhenFolderHasOlderVersionOrOtherPublicKeyToken(string request)
        {
            string path = CopyAssembly(typeof(Sdk).Assembly, codeFolder);

            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(request)));
            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(request)));

            Assert.Empty(loads);
            _ = AssertSingleTrace(MetricsTraceLevel.Warning, $"Service Fabric does not supply '{path}' (");
        }

        [Fact]
        public void DeclinesAssemblyMissingFromCodeFolder()
        {
            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest)));

            Assert.Empty(loads);
            Assert.Empty(scope.TracesOfType(TraceType));
        }

        [Fact]
        public void DeclinesCultureSpecificRequest()
        {
            _ = CopyAssembly(typeof(Sdk).Assembly, codeFolder);

            Assert.Null(resolver.Resolve(null, new ResolveEventArgs("OpenTelemetry, Version=1.0.0.0, Culture=de, PublicKeyToken=7bd6737fe5b67e3c")));

            Assert.Empty(loads);
        }

        [Fact]
        public void DeclinesRequestsOfAppCode()
        {
            _ = CopyAssembly(typeof(Sdk).Assembly, codeFolder);

            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest, typeof(Assert).Assembly)));
            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest, new FakeAssembly(Path.Combine(appDirectory, "App.dll")))));
            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest, new FakeAssembly(Path.Combine(codeFolder, "Dynamic.dll"), isDynamic: true))));

            Assert.Empty(loads);
            Assert.Empty(scope.TracesOfType(TraceType));
        }

        [Fact]
        public void SuppliesToAssembliesLoadedFromCodeFolder()
        {
            string path = CopyAssembly(typeof(Sdk).Assembly, codeFolder);
            OpenTelemetryAssemblyResolver withTrailingSeparator = CreateResolver(codeFolder + Path.DirectorySeparatorChar);

            Assert.Same(loaded, withTrailingSeparator.Resolve(
                null, new ResolveEventArgs(OpenTelemetryRequest, new FakeAssembly(Path.Combine(codeFolder.ToUpperInvariant(), "OpenTelemetry.Exporter.Geneva.dll")))));

            Assert.Equal([path], loads);
        }

#if NETFRAMEWORK
        [Fact]
        public void SuppliesToAssembliesInGlobalAssemblyCache()
        {
            string path = CopyAssembly(typeof(Sdk).Assembly, codeFolder);

            Assert.Same(loaded, resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest, typeof(object).Assembly)));

            Assert.Equal([path], loads);
        }
#endif

        [Fact]
        public void TracesEachSuppliedAssemblyOnce()
        {
            _ = CopyAssembly(typeof(Sdk).Assembly, codeFolder);

            _ = resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest));
            _ = resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest));

            Assert.Equal(2, loads.Count);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, "Service Fabric supplied ");
        }

        [Fact]
        public void DoesNotRecurseWhenTracingRequestsTheSuppliedAssemblyAgain()
        {
            _ = CopyAssembly(typeof(Sdk).Assembly, codeFolder);
            Action<MetricsTraceLevel, string, string> recordTrace = MetricsTrace.Sink;
            int traces = 0;
            Assembly nested = null;
            MetricsTrace.Sink = (level, type, message) =>
            {
                recordTrace(level, type, message);

                // Like a trace sink that needs the assembly being supplied, which raises AssemblyResolve for it again. Recurses at
                // most once, so a missing guard fails the assertions below rather than overflowing the stack.
                if (traces++ == 0)
                    nested = resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest));
            };

            Assembly assembly = resolver.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest));

            Assert.Same(loaded, assembly);
            Assert.Same(loaded, nested);
            Assert.Equal(1, traces);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, "Service Fabric supplied ");
        }

        [Fact]
        public void ReturnsNullAndTracesWarningWhenLoadingThrows()
        {
            _ = CopyAssembly(typeof(Sdk).Assembly, codeFolder);
            OpenTelemetryAssemblyResolver failing = new(codeFolder, ServiceFabricMetrics, AssemblyName.GetAssemblyName, _ => throw new BadImageFormatException());

            Assert.Null(failing.Resolve(null, new ResolveEventArgs(OpenTelemetryRequest)));

            _ = AssertSingleTrace(MetricsTraceLevel.Warning, $"Service Fabric failed to supply {OpenTelemetryRequest} from '{codeFolder}'.");
        }
    }

    /// <summary>
    /// Tests resolving in an app that ships OpenTelemetry: the app's newer copies stand in for the exact versions Service Fabric's
    /// metrics reference, and only assemblies the app lacks come from the fill-in folder. Empty files stand in for the app's copies,
    /// with the assembly names the tests give them.
    /// </summary>
    public sealed class ResolveForAppOpenTelemetry : OpenTelemetryAssemblyResolverTest
    {
        readonly Dictionary<string, AssemblyName> fileNames = new(StringComparer.OrdinalIgnoreCase);
        readonly List<string> appLoads = [];
        readonly List<string> folderLoads = [];
        readonly Assembly appAssembly = typeof(object).Assembly;
        readonly Assembly folderAssembly = typeof(Assert).Assembly;
        readonly string fillInFolder;
        readonly AssemblyName genevaReference =
            typeof(OpenTelemetryAssemblyResolver).Assembly.GetReferencedAssemblies().Single(r => r.Name == "OpenTelemetry.Exporter.Geneva");

        public ResolveForAppOpenTelemetry() =>
            fillInFolder = Directory.CreateDirectory(Path.Combine(codeFolder, OpenTelemetryAssemblyResolver.FillInFolderName)).FullName;

        Version Newer => new(genevaReference.Version.Major, genevaReference.Version.Minor + 1, 2, 1008);

        string Request => genevaReference.FullName;

        OpenTelemetryAssemblyResolver CreateResolver(string folder, Func<AssemblyName, Assembly> load = null) => new(
            folder,
            typeof(OpenTelemetryAssemblyResolver).Assembly,
            path => fileNames.TryGetValue(path, out AssemblyName name) ? name : AssemblyName.GetAssemblyName(path),
            path =>
            {
                folderLoads.Add(path);
                return folderAssembly;
            },
            [appDirectory],
            load ?? (name =>
            {
                appLoads.Add(name.FullName);
                return appAssembly;
            }));

        AssemblyName GenevaName(Version version, byte[] publicKeyToken = null)
        {
            AssemblyName name = new(genevaReference.FullName) { Version = version };
            if (publicKeyToken != null)
                name.SetPublicKeyToken(publicKeyToken);

            return name;
        }

        AssemblyName CreateAppGeneva(string directory, AssemblyName name)
        {
            string path = Path.Combine(directory, genevaReference.Name + ".dll");
            CreateFile(directory, genevaReference.Name + ".dll");
            fileNames[path] = name;
            return name;
        }

        static AssemblyName Reference(string name) =>
            typeof(OpenTelemetryAssemblyResolver).Assembly.GetReferencedAssemblies().Single(r => r.Name == name);

        AssemblyName CreateAppCopy(AssemblyName reference, int majorVersionChange)
        {
            AssemblyName name = new(reference.FullName) { Version = new Version(reference.Version.Major + majorVersionChange, 0, 0, 0) };
            CreateFile(appDirectory, name.Name + ".dll");
            fileNames[Path.Combine(appDirectory, name.Name + ".dll")] = name;
            return name;
        }

        [Fact]
        public void LetsNewerAppCopyStandInForReferencedVersion()
        {
            AssemblyName app = CreateAppGeneva(appDirectory, GenevaName(Newer));
            _ = CopyAssembly(typeof(GenevaMetricExporter).Assembly, fillInFolder);

            Assembly assembly = CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(Request));

            Assert.Same(appAssembly, assembly);
            Assert.Equal([app.FullName], appLoads);
            Assert.Empty(folderLoads);
            _ = AssertSingleTrace(
                MetricsTraceLevel.Info, $"The app's {appAssembly.FullName} stands in for {Request}, which Service Fabric's metrics reference, as a binding redirect would.");
        }

        [Fact]
        public void UsesNewestLoadedAppCopyWithoutLoading()
        {
            _ = CreateAppGeneva(appDirectory, GenevaName(new Version(genevaReference.Version.Major, 0, 0, 1)));
            FakeAssembly older = new(Path.Combine(appDirectory, "older", "Geneva.dll"), fullName: GenevaName(Newer).FullName);
            FakeAssembly newest = new(Path.Combine(appDirectory, "newest", "Geneva.dll"), fullName: GenevaName(new Version(genevaReference.Version.Major, 99, 0, 0)).FullName);
            OpenTelemetryAssemblyResolver.ReadLoadedAssemblies = () => [typeof(object).Assembly, older, newest];

            Assert.Same(newest, CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(Request)));

            Assert.Empty(appLoads);
            Assert.Empty(folderLoads);
        }

        [Fact]
        public void LetsLoadedAppCopyOfReferencedVersionStandIn()
        {
            FakeAssembly loaded = new(Path.Combine(appDirectory, "plugins", "Geneva.dll"), fullName: Request);
            OpenTelemetryAssemblyResolver.ReadLoadedAssemblies = () => [typeof(object).Assembly, loaded];

            Assert.Same(loaded, CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(Request)));

            Assert.Empty(appLoads);
        }

        [Fact]
        public void IgnoresCopiesLoadedFromFillInFolder()
        {
            FakeAssembly suppliedEarlier = new(Path.Combine(fillInFolder, "OpenTelemetry.Exporter.Geneva.dll"), fullName: GenevaName(Newer).FullName);
            OpenTelemetryAssemblyResolver.ReadLoadedAssemblies = () => [typeof(object).Assembly, suppliedEarlier];
            string path = CopyAssembly(typeof(GenevaMetricExporter).Assembly, fillInFolder);

            Assert.Same(folderAssembly, CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(Request)));

            Assert.Equal([path], folderLoads);
        }

        [Theory]
        [InlineData("older")]
        [InlineData("next major")]
        [InlineData("referenced version")]
        [InlineData("other public key token")]
        public void DeclinesAppCopyThatCannotStandInAndSuppliesNothing(string appCopy)
        {
            Version referenced = genevaReference.Version;
            AssemblyName name = appCopy switch
            {
                "older" => GenevaName(new Version(referenced.Major, 0, 0, 1)),
                "next major" => GenevaName(new Version(referenced.Major + 1, 0, 0, 0)),
                "referenced version" => GenevaName(referenced),
                _ => GenevaName(Newer, [1, 2, 3, 4, 5, 6, 7, 8]),
            };
            _ = CreateAppGeneva(appDirectory, name);
            _ = CopyAssembly(typeof(GenevaMetricExporter).Assembly, fillInFolder);
            OpenTelemetryAssemblyResolver resolver = CreateResolver(fillInFolder);

            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(Request)));
            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(Request)));

            Assert.Empty(appLoads);
            Assert.Empty(folderLoads);
            _ = AssertSingleTrace(MetricsTraceLevel.Warning, $"The app's {name.FullName} does not stand in for {Request}, which Service Fabric's metrics reference,");
        }

        [Theory]
        [InlineData("System.Diagnostics.DiagnosticSource")]
        [InlineData("Microsoft.Extensions.Configuration")]
        [InlineData("Microsoft.Extensions.Configuration.Abstractions")]
        [InlineData("Microsoft.Extensions.DependencyInjection.Abstractions")]
        public void LetsNewerMajorVersionOfDiagnosticSourceAndMicrosoftExtensionsStandIn(string assemblyName)
        {
            AssemblyName reference = Reference(assemblyName);
            AssemblyName app = CreateAppCopy(reference, majorVersionChange: 1);

            Assert.Same(appAssembly, CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(reference.FullName)));

            Assert.Equal([app.FullName], appLoads);
            Assert.Empty(folderLoads);
        }

        [Theory]
        [InlineData("System.Diagnostics.DiagnosticSource")]
        [InlineData("Microsoft.Extensions.Configuration")]
        public void DeclinesOlderMajorVersionOfDiagnosticSourceAndMicrosoftExtensions(string assemblyName)
        {
            AssemblyName reference = Reference(assemblyName);
            AssemblyName app = CreateAppCopy(reference, majorVersionChange: -1);

            Assert.Null(CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(reference.FullName)));

            Assert.Empty(appLoads);
            Assert.Empty(folderLoads);
            _ = AssertSingleTrace(MetricsTraceLevel.Warning, $"The app's {app.FullName} does not stand in for {reference.FullName},");
        }

        [Fact]
        public void FindsAppCopyInProbingOrder()
        {
            string first = Directory.CreateDirectory(Path.Combine(appDirectory, "first")).FullName;
            string second = Directory.CreateDirectory(Path.Combine(appDirectory, "second")).FullName;
            AssemblyName older = CreateAppGeneva(first, GenevaName(new Version(genevaReference.Version.Major, 0, 0, 1)));
            _ = CreateAppGeneva(second, GenevaName(Newer));
            OpenTelemetryAssemblyResolver resolver = new(
                fillInFolder,
                typeof(OpenTelemetryAssemblyResolver).Assembly,
                path => fileNames[path],
                path =>
                {
                    folderLoads.Add(path);
                    return folderAssembly;
                },
                [Path.Combine(root, "missing"), first, second],
                name =>
                {
                    appLoads.Add(name.FullName);
                    return appAssembly;
                });

            Assert.Null(resolver.Resolve(null, new ResolveEventArgs(Request)));

            Assert.Empty(appLoads);
            Assert.Empty(folderLoads);
            _ = AssertSingleTrace(MetricsTraceLevel.Warning, $"The app's {older.FullName} does not stand in for {Request},");
        }

        [Theory]
        [InlineData("other version")]
        [InlineData("other public key token")]
        [InlineData("no version")]
        [InlineData("not referenced")]
        public void DeclinesRequestThatIsNotAnExactReference(string request)
        {
            _ = CreateAppGeneva(appDirectory, GenevaName(Newer));
            CreateFile(appDirectory, "Moq.dll");
            fileNames[Path.Combine(appDirectory, "Moq.dll")] = new(typeof(Mock).Assembly.GetName().FullName) { Version = new Version(99, 0) };
            string name = request switch
            {
                "other version" => GenevaName(new Version(genevaReference.Version.Major, genevaReference.Version.Minor, genevaReference.Version.Build, genevaReference.Version.Revision + 1)).FullName,
                "other public key token" => GenevaName(genevaReference.Version, [1, 2, 3, 4, 5, 6, 7, 8]).FullName,
                "no version" => genevaReference.Name,
                _ => typeof(Mock).Assembly.GetName().FullName,
            };

            Assert.Null(CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(name)));

            Assert.Empty(appLoads);
            Assert.Empty(folderLoads);
            Assert.Empty(scope.TracesOfType(TraceType));
        }

        [Fact]
        public void DeclinesRequestsOfAppCode()
        {
            _ = CreateAppGeneva(appDirectory, GenevaName(Newer));

            Assert.Null(CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(Request, new FakeAssembly(Path.Combine(appDirectory, "App.dll")))));

            Assert.Empty(appLoads);
        }

        [Fact]
        public void DeclinesCultureSpecificRequest()
        {
            _ = CreateAppGeneva(appDirectory, GenevaName(Newer));
            AssemblyName request = new(Request) { CultureName = "de" };

            Assert.Null(CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(request.FullName)));

            Assert.Empty(appLoads);
        }

        [Fact]
        public void SuppliesAssemblyTheAppLacksFromFillInFolder()
        {
            string path = CopyAssembly(typeof(GenevaMetricExporter).Assembly, fillInFolder);

            Assert.Same(folderAssembly, CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(Request)));

            Assert.Equal([path], folderLoads);
            Assert.Empty(appLoads);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, $"Service Fabric supplied '{path}' ({folderAssembly.FullName}) for {Request}.");
        }

        [Fact]
        public void SuppliesNothingTheAppLacksWithoutFillInFolderOrFile()
        {
            Assert.Null(CreateResolver(null).Resolve(null, new ResolveEventArgs(Request)));
            Assert.Null(CreateResolver(fillInFolder).Resolve(null, new ResolveEventArgs(Request)));

            Assert.Empty(folderLoads);
            Assert.Empty(scope.TracesOfType(TraceType));
        }

        [Fact]
        public void ReturnsNullAndTracesWarningWhenLoadingAppCopyThrows()
        {
            _ = CreateAppGeneva(appDirectory, GenevaName(Newer));
            OpenTelemetryAssemblyResolver failing = CreateResolver(fillInFolder, _ => throw new FileLoadException());

            Assert.Null(failing.Resolve(null, new ResolveEventArgs(Request)));

            _ = AssertSingleTrace(MetricsTraceLevel.Warning, $"Service Fabric failed to resolve {Request} from the app's assemblies or '{fillInFolder}'.");
        }
    }

    public sealed class ReadConfigurationPrivatePath : OpenTelemetryAssemblyResolverTest
    {
        string WriteConfiguration(string content)
        {
            string path = Path.Combine(appDirectory, "App.exe.config");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void ReturnsProbingPrivatePaths()
        {
            string path = WriteConfiguration(
                """
                <configuration>
                  <runtime>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                      <probing privatePath="bin;lib\otel" />
                    </assemblyBinding>
                    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                      <probing privatePath="plugins" />
                    </assemblyBinding>
                    <assemblyBinding>
                      <probing privatePath="ignored-without-namespace" />
                    </assemblyBinding>
                  </runtime>
                </configuration>
                """);

            Assert.Equal(@"bin;lib\otel;plugins", OpenTelemetryAssemblyResolver.ReadConfigurationPrivatePath(path));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Missing.exe.config")]
        public void ReturnsNullWithoutConfigurationFile(string fileName)
        {
            string path = string.IsNullOrEmpty(fileName) ? fileName : Path.Combine(appDirectory, fileName);

            Assert.Null(OpenTelemetryAssemblyResolver.ReadConfigurationPrivatePath(path));
        }

        [Fact]
        public void ReturnsEmptyWithoutProbing()
        {
            string path = WriteConfiguration("<configuration><runtime /></configuration>");

            Assert.Equal(string.Empty, OpenTelemetryAssemblyResolver.ReadConfigurationPrivatePath(path));
        }

        [Fact]
        public void ThrowsWhenConfigurationFileIsMalformed()
        {
            string path = WriteConfiguration("<configuration><runtime>");

            _ = Assert.Throws<XmlException>(() => OpenTelemetryAssemblyResolver.ReadConfigurationPrivatePath(path));
        }

        [Fact]
        public void ProbesApplicationBaseThenPrivateBinPathThenConfigurationPrivatePath()
        {
            string configuration = WriteConfiguration(
                """<configuration><runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1"><probing privatePath="plugins" /></assemblyBinding></runtime></configuration>""");

            Assert.Equal(
                [appDirectory, Path.Combine(appDirectory, "bin"), Path.Combine(appDirectory, @"lib\otel"), Path.Combine(appDirectory, "plugins")],
                OpenTelemetryAssemblyResolver.GetProbingDirectories(appDirectory, @"bin; lib\otel ;;", configuration).ToArray());
            Assert.Equal([appDirectory], OpenTelemetryAssemblyResolver.GetProbingDirectories(appDirectory, null, null).ToArray());
        }

#if NETFRAMEWORK
        [Fact]
        public void ReadsProbingDirectoriesOfTheAppDomain()
        {
            // .NET Framework leaves the configuration file's probing private path out of AppDomain.RelativeSearchPath.
            string configuration = WriteConfiguration(
                """<configuration><runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1"><probing privatePath="config-lib" /></assemblyBinding></runtime></configuration>""");
            AppDomainSetup setup = new()
            {
                ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                PrivateBinPath = "private-bin",
                ConfigurationFile = configuration,
            };
            AppDomain domain = AppDomain.CreateDomain(nameof(ReadsProbingDirectoriesOfTheAppDomain), null, setup);
            try
            {
                domain.DoCallBack(ReadProbingDirectoriesOfCurrentAppDomain);
                string baseDirectory = (string)domain.GetData("BaseDirectory");

                Assert.Equal(
                    [baseDirectory, Path.Combine(baseDirectory, "private-bin"), Path.Combine(baseDirectory, "config-lib")],
                    (string[])domain.GetData("ProbingDirectories"));
            }
            finally
            {
                AppDomain.Unload(domain);
            }
        }

        static void ReadProbingDirectoriesOfCurrentAppDomain()
        {
            AppDomain.CurrentDomain.SetData("BaseDirectory", AppDomain.CurrentDomain.BaseDirectory);
            AppDomain.CurrentDomain.SetData("ProbingDirectories", OpenTelemetryAssemblyResolver.ReadAppProbingDirectories().ToArray());
        }
#endif
    }
}
