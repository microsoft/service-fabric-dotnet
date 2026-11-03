// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

#if !NET
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Inspector;
using OpenTelemetry.Exporter.Geneva;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

[Collection(MetricsPipelineCollection.Name)]
public abstract class OpenTelemetryAssemblyResolverTest : IDisposable
{
    readonly MetricsTestScope scope = new();

    // Outside the application base, so only the resolver finds the assemblies saved here.
    readonly string folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;

    void IDisposable.Dispose()
    {
        scope.Dispose();
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A loaded assembly keeps its file until the process exits.
        }
    }

    public sealed class Register : OpenTelemetryAssemblyResolverTest
    {
        [Fact]
        public void LoadsAssemblyTheAppDoesNotHaveFromFolderOnceOpenTelemetryStarts()
        {
            string name = SaveAssembly("1.2.3.4");
            OpenTelemetryAssemblyResolver.GetFolder = () => folder;

            Assert.Equal(MetricsBackend.OpenTelemetry, MetricsBackendSelector.Backend);

            AssertLoadedFromFolder(name, Assembly.Load(name + ", Version=1.2.3.4"));
        }

        [Fact]
        public void KeepsOpenTelemetryWhenCodePathCannotBeRead()
        {
            OpenTelemetryAssemblyResolver.GetFolder = () => throw new DllNotFoundException();

            Assert.Equal(MetricsBackend.OpenTelemetry, MetricsBackendSelector.Backend);
        }
    }

    public sealed class Resolve : OpenTelemetryAssemblyResolverTest
    {
        // The app's copy: an assembly in the application base, newer than the requested version unless a test changes it.
        static readonly Assembly appCopy = typeof(GenevaMetricExporter).Assembly;

        readonly AssemblyName requested = new(appCopy.FullName) { Version = new Version(1, 0, 0, 0) };

        [Fact]
        public void ReturnsNewerAppCopyInsteadOfFile()
        {
            _ = SaveAssembly(requested.Version.ToString(), requested.Name);

            Assert.Same(appCopy, OpenTelemetryAssemblyResolver.Resolve(folder, requested.FullName));
            // Also on the next request, after loading the app's copy.
            Assert.Same(appCopy, OpenTelemetryAssemblyResolver.Resolve(folder, requested.FullName));
        }

        [Theory]
        [InlineData("1.2.3.4")]
        [InlineData("1.2.0.0")]
        public void LoadsFileWithRequestedOrNewerVersion(string requestedVersion)
        {
            string name = SaveAssembly("1.2.3.4");

            AssertLoadedFromFolder(name, OpenTelemetryAssemblyResolver.Resolve(folder, $"{name}, Version={requestedVersion}"));
        }

        [Fact]
        public void ReturnsNullForOlderFile()
        {
            string name = SaveAssembly("1.2.3.4");

            Assert.Null(OpenTelemetryAssemblyResolver.Resolve(folder, $"{name}, Version=1.2.3.5"));
        }

        [Fact]
        public void ReturnsNullWithoutFile() =>
            Assert.Null(OpenTelemetryAssemblyResolver.Resolve(folder, "Missing, Version=1.0.0.0"));

        [Fact]
        public void IgnoresAppCopyOfAssemblyMissingFromFolder() =>
            Assert.Null(OpenTelemetryAssemblyResolver.Resolve(folder, requested.FullName));

        [Fact]
        public void IgnoresAppCopyWhileLoadingAppCopy()
        {
            _ = SaveAssembly("0.0.0.1", requested.Name);
            var loadingAppCopy = typeof(OpenTelemetryAssemblyResolver).Field<bool>();
            loadingAppCopy.Set(true);
            try
            {
                Assert.Null(OpenTelemetryAssemblyResolver.Resolve(folder, requested.FullName));
            }
            finally
            {
                loadingAppCopy.Set(false);
            }
        }

        [Fact]
        public void IgnoresAppCopyThatIsNotNewer()
        {
            requested.Version = appCopy.GetName().Version;
            _ = SaveAssembly("1.0.0.0", requested.Name);

            Assert.Null(OpenTelemetryAssemblyResolver.Resolve(folder, requested.FullName));
        }

        [Fact]
        public void IgnoresAppCopyWithAnotherPublicKeyToken()
        {
            requested.SetPublicKeyToken([.. requested.GetPublicKeyToken().Select(static b => (byte)~b)]);
            _ = SaveAssembly("0.0.0.1", requested.Name);

            Assert.Null(OpenTelemetryAssemblyResolver.Resolve(folder, requested.FullName));
        }
    }

    /// <summary>
    /// Saves an empty assembly with the given name, or a name used by no other test, to the folder and returns the name.
    /// </summary>
    string SaveAssembly(string version, string name = null)
    {
        name ??= MetricsTestScope.UniqueName("ResolverTest" + Guid.NewGuid().ToString("N"));
        AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName(name) { Version = new Version(version) }, AssemblyBuilderAccess.Save, folder);
        _ = assembly.DefineDynamicModule(name, name + ".dll");
        assembly.Save(name + ".dll");
        return name;
    }

    static void AssertLoadedFromFolder(string name, Assembly assembly)
    {
        Assert.False(assembly.IsDynamic);
        Assert.Equal(new Version(1, 2, 3, 4), assembly.GetName().Version);
        Assert.Equal(name + ".dll", Path.GetFileName(assembly.Location), ignoreCase: true);
    }
}
#endif
