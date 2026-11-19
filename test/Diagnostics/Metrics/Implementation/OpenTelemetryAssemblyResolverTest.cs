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
        public void SuppliesNothingWhenAppShipsAnyOpenTelemetryAssembly(string fileName)
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            CreateFile(appDirectory, fileName);

            OpenTelemetryAssemblyResolver.Install();

            Assert.Empty(handlers);
            TraceRecord trace = AssertSingleTrace(MetricsTraceLevel.Info, "The app provides OpenTelemetry (");
            Assert.Contains(Path.Combine(appDirectory, fileName), trace.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SuppliesNothingWhenAnyProbingDirectoryHasOpenTelemetryAssembly()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            string privateDirectory = Directory.CreateDirectory(Path.Combine(appDirectory, "lib")).FullName;
            CreateFile(privateDirectory, "OpenTelemetry.Api.dll");
            OpenTelemetryAssemblyResolver.ReadAppProbingDirectories = () => [appDirectory, Path.Combine(root, "missing"), privateDirectory];

            OpenTelemetryAssemblyResolver.Install();

            Assert.Empty(handlers);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, "The app provides OpenTelemetry (");
        }

        [Fact]
        public void SuppliesNothingWhenOpenTelemetryAssemblyIsLoaded()
        {
            CreateFile(codeFolder, "OpenTelemetry.dll");
            OpenTelemetryAssemblyResolver.ReadLoadedAssemblies = () => [typeof(object).Assembly, typeof(Baggage).Assembly];

            OpenTelemetryAssemblyResolver.Install();

            Assert.Empty(handlers);
            _ = AssertSingleTrace(MetricsTraceLevel.Info, $"The app provides OpenTelemetry (loaded assembly {typeof(Baggage).Assembly.FullName}),");
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

        sealed class FakeAssembly(string location, bool isDynamic = false, AssemblyName[] references = null) : Assembly
        {
            public override string Location => location;

            public override bool IsDynamic => isDynamic;

#if NETFRAMEWORK
            public override bool GlobalAssemblyCache => false;
#endif

            public override string FullName => "Fake, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

            public override AssemblyName GetName() => new(FullName);

            public override AssemblyName[] GetReferencedAssemblies() => references ?? [];
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
