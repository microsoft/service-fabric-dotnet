// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

#if !NET
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
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
    }

    /// <summary>
    /// Saves an empty assembly with a name used by no other test to the folder and returns the name.
    /// </summary>
    string SaveAssembly(string version)
    {
        string name = MetricsTestScope.UniqueName("ResolverTest" + Guid.NewGuid().ToString("N"));
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
