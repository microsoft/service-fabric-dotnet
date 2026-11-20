// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;
using DiagnosticsMeter = System.Diagnostics.Metrics.Meter;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

public abstract class SfOpenTelemetrySdkTest : IDisposable
{
    readonly MetricsTestScope scope = new();

    void IDisposable.Dispose() => ((IDisposable)scope).Dispose();

    public sealed class Build : SfOpenTelemetrySdkTest
    {
        [Fact]
        public void SkipsCollectorsWithInvalidConnectionStrings()
        {
            _ = scope.Source.SetCollectors("Account=OnlyAccount", MetricsTestScope.ValidConnectionString, "Endpoint=tcp://host:1;Account=a;Namespace=n");
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Collectors");

            provider.CreateMeter("Namespace", name).Record(1);

            Assert.Equal(MetricsTestScope.ValidConnectionString, scope.Exporter.ConnectionString);
            _ = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Equal(2, scope.TracesOfType("OpenTelemetryMetricsFactory").Count(t => t.Message.Contains("skipping this collector")));
        }

        [Fact]
        public void ReturnsNullWithoutValidCollector()
        {
            _ = scope.Source.SetCollectors("Account=OnlyAccount");
            MetricsSettings settings = MetricsSettings.Load(scope.Source);

            Assert.Null(SfOpenTelemetrySdk.Build(settings));
            Assert.Contains(scope.TracesOfType("OpenTelemetryMetricsFactory"), t => t.Message.Contains("No valid Geneva collector"));
        }

        [Fact]
        public void ExportsSameDataToEachValidCollector()
        {
            _ = scope.Source.SetCollectors(MetricsTestScope.ValidConnectionString, MetricsTestScope.ValidConnectionString.Replace("TestAccount", "SecondAccount"));
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("TwoCollectors");

            provider.CreateMeter("Namespace", name).Record(3);
            SfMetricsPipeline.ForceFlush();

            Assert.Equal(2, scope.Exporters.Count);
            foreach (TestMetricExporter exporter in scope.Exporters)
                Assert.Equal(3d, exporter.Points.Single(p => p.MetricName == "Namespace." + name).Sum);
        }

        [Theory, InlineData(typeof(ArgumentException)), InlineData(typeof(NotSupportedException))]
        public void SkipsCollectorRejectedByExporter(Type errorType)
        {
            string rejected = MetricsTestScope.ValidConnectionString.Replace("TestAccount", "Rejected");
            _ = scope.Source.SetCollectors(rejected, MetricsTestScope.ValidConnectionString);
            Func<string, IReadOnlyDictionary<string, object>, BaseExporter<Metric>> createTestExporter = SfOpenTelemetrySdk.CreateExporter;
            SfOpenTelemetrySdk.CreateExporter = (connectionString, dimensions) => connectionString == rejected
                ? throw (Exception)Activator.CreateInstance(errorType, "rejected by the exporter")
                : createTestExporter(connectionString, dimensions);
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Rejected");

            provider.CreateMeter("Namespace", name).Record(1);

            _ = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Contains(scope.TracesOfType("OpenTelemetryMetricsFactory"), t => t.Message.Contains("rejected by the exporter"));
        }

        [Fact]
        public void ThrowsWhenExporterRejectsEveryCollector()
        {
            ArgumentException rejection = new();
            SfOpenTelemetrySdk.CreateExporter = (_, _) => throw rejection;
            MetricsSettings settings = MetricsSettings.Load(scope.Source);

            NotSupportedException error = Assert.Throws<NotSupportedException>(() => SfOpenTelemetrySdk.Build(settings));

            Assert.Same(rejection, error.InnerException);
        }

        [Fact]
        public void ThrowsExporterErrorOtherThanRejectedConnectionString()
        {
            FileNotFoundException error = new();
            SfOpenTelemetrySdk.CreateExporter = (_, _) => throw error;
            MetricsSettings settings = MetricsSettings.Load(scope.Source);

            Assert.Same(error, Assert.Throws<FileNotFoundException>(() => SfOpenTelemetrySdk.Build(settings)));
        }

        [Fact]
        public void DisposesExportersOfEarlierCollectorsWhenItThrows()
        {
            string failing = MetricsTestScope.ValidConnectionString.Replace("TestAccount", "Failing");
            _ = scope.Source.SetCollectors(MetricsTestScope.ValidConnectionString, failing);
            Func<string, IReadOnlyDictionary<string, object>, BaseExporter<Metric>> createTestExporter = SfOpenTelemetrySdk.CreateExporter;
            SfOpenTelemetrySdk.CreateExporter = (connectionString, dimensions) =>
                connectionString == failing ? throw new FileNotFoundException() : createTestExporter(connectionString, dimensions);
            MetricsSettings settings = MetricsSettings.Load(scope.Source);

            _ = Assert.Throws<FileNotFoundException>(() => SfOpenTelemetrySdk.Build(settings));
            Assert.True(scope.Exporter.Disposed);
        }

        [Fact]
        public void GivesExporterFilteredNodeDimensions()
        {
            _ = scope.Source
                .Set("Telemetry/Metrics/Dimensions", "Region", "westus")
                .Set("Telemetry/Metrics/Dimensions", "_Microsoft_Metrics_Account", "reserved")
                .Set("Telemetry/Metrics/Dimensions", "Long", new string('v', 1025))
                .Set("FabricNode", "InstanceName", "_Node_0");

            using (new Int64MeterProvider())
            {
                IReadOnlyDictionary<string, object> dimensions = scope.Exporter.PrepopulatedDimensions;
                Assert.Equal("westus", dimensions["Region"]);
                Assert.Equal("_Node_0", dimensions["NodeName"]);
                Assert.Contains("RuntimeVersion", dimensions);
                Assert.Contains("OsType", dimensions);
                Assert.Contains("Architecture", dimensions);
                Assert.Equal("Unknown", dimensions["Microsoft.ResourceId"]);
                Assert.DoesNotContain("_Microsoft_Metrics_Account", dimensions);
                Assert.DoesNotContain("Long", dimensions);
            }
        }

        [Fact]
        public void IgnoresOtelSdkDisabledEnvironmentVariable()
        {
            string original = Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED");
            Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", "true");
            try
            {
                using Int64MeterProvider provider = new();
                string name = MetricsTestScope.UniqueName("OtelSdkDisabled");

                provider.CreateMeter("Namespace", name).Record(1);

                _ = Assert.Single(scope.FlushAndGet("Namespace." + name));
            }
            finally
            {
                Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", original);
            }
        }

        [Fact]
        public void ExportsOnlyInstrumentsOfServiceFabricMeterObject()
        {
            using DiagnosticsMeter foreignMeter = new("Microsoft.ServiceFabric", "1.0");
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Identity");

            foreignMeter.CreateHistogram<long>("Namespace.Foreign" + name).Record(1);
            provider.CreateMeter("Namespace", name).Record(1);

            _ = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.DoesNotContain(scope.Exporter.Points, p => p.MetricName == "Namespace.Foreign" + name);
        }
    }

    public sealed class EnsureSupportedVersion : SfOpenTelemetrySdkTest
    {
        [Theory]
        [InlineData("OpenTelemetry")]
        [InlineData("OpenTelemetry.Api")]
        [InlineData("OpenTelemetry.Api.ProviderBuilderExtensions")]
        public void ThrowsWhenAnOpenTelemetryAssemblyIsOlderThanSupported(string assemblyName)
        {
            SfOpenTelemetrySdk.ReadFileVersion = assembly =>
                assembly.GetName().Name == assemblyName ? new Version(1, 15, 2, 2002) : new Version(1, 15, 3, 0);

            NotSupportedException error = Assert.Throws<NotSupportedException>(SfOpenTelemetrySdk.EnsureSupportedVersion);

            Assert.Contains($"{assemblyName} 1.15.2.2002 loaded from ", error.Message);
            Assert.Contains("is older than OpenTelemetry 1.15.3,", error.Message);
        }

        [Theory]
        [InlineData("OpenTelemetry")]
        [InlineData("OpenTelemetry.Api")]
        [InlineData("OpenTelemetry.Api.ProviderBuilderExtensions")]
        [InlineData("OpenTelemetry.Exporter.Geneva")]
        public void ThrowsWhenAnOpenTelemetryAssemblyOrGenevaExporterHasNewerMajorVersion(string assemblyName)
        {
            SfOpenTelemetrySdk.ReadFileVersion = assembly =>
                assembly.GetName().Name == assemblyName ? new Version(2, 0, 0, 0) : new Version(1, 15, 3, 0);

            NotSupportedException error = Assert.Throws<NotSupportedException>(SfOpenTelemetrySdk.EnsureSupportedVersion);

            Assert.Contains($"{assemblyName} 2.0.0.0 loaded from ", error.Message);
        }

        [Theory]
        [InlineData("1.15.3.0")]
        [InlineData("1.20.1.1")]
        [InlineData(null)]
        public void AcceptsRequiredNewerOrUnknownVersions(string version)
        {
            SfOpenTelemetrySdk.ReadFileVersion = _ => version == null ? null : new Version(version);

            SfOpenTelemetrySdk.EnsureSupportedVersion();
        }
    }

    public sealed class ReadFileVersion : SfOpenTelemetrySdkTest
    {
        [Fact]
        public void ReturnsAssemblyFileVersion()
        {
            Assembly openTelemetry = typeof(Sdk).Assembly;

            Version version = SfOpenTelemetrySdk.ReadFileVersion(openTelemetry);

            Assert.Equal(openTelemetry.GetCustomAttribute<AssemblyFileVersionAttribute>().Version, version.ToString());
        }

        [Fact]
        public void ReturnsAssemblyFileVersionOfAssemblyLoadedFromBytes()
        {
            // An assembly without a file stands in for one whose file FileVersionInfo cannot read, such as at a path of 260 or more
            // characters.
            Assembly openTelemetry = Assembly.Load(File.ReadAllBytes(typeof(Sdk).Assembly.Location));

            Version version = SfOpenTelemetrySdk.ReadFileVersion(openTelemetry);

            Assert.Equal(openTelemetry.GetCustomAttribute<AssemblyFileVersionAttribute>().Version, version?.ToString());
        }

        [Fact]
        public void ReturnsNullForAssemblyWithoutFileVersion() =>
            Assert.Null(SfOpenTelemetrySdk.ReadFileVersion(AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("NoFile"), AssemblyBuilderAccess.Run)));
    }

    public sealed class ForceFlush : SfOpenTelemetrySdkTest
    {
        [Fact]
        public void TracesWarningForFirstIncompleteFlush()
        {
            using Int64MeterProvider provider = new();
            IMeter<long> meter = provider.CreateMeter("Namespace", MetricsTestScope.UniqueName("IncompleteFlush"));
            scope.Exporter.Fails = true;
            try
            {
                meter.Record(1);
                SfMetricsPipeline.ForceFlush();
                meter.Record(2);
                SfMetricsPipeline.ForceFlush();

                TraceRecord warning = Assert.Single(scope.TracesOfType("OpenTelemetryMetricsFactory"), t => t.Level == MetricsTraceLevel.Warning);
                Assert.Contains("did not complete for every Geneva collector (1 incomplete flush(es)", warning.Message);
            }
            finally
            {
                // A complete flush resets the process-wide throttle for the next test.
                scope.Exporter.Fails = false;
                SfMetricsPipeline.ForceFlush();
            }
        }

        [Fact]
        public void TracesInfoForFirstCompleteFlushAfterIncompleteOnes()
        {
            using Int64MeterProvider provider = new();
            IMeter<long> meter = provider.CreateMeter("Namespace", MetricsTestScope.UniqueName("RecoveredFlush"));
            scope.Exporter.Fails = true;
            meter.Record(1);
            SfMetricsPipeline.ForceFlush();
            meter.Record(2);
            SfMetricsPipeline.ForceFlush();
            scope.Exporter.Fails = false;

            meter.Record(3);
            SfMetricsPipeline.ForceFlush();
            meter.Record(4);
            SfMetricsPipeline.ForceFlush();

            TraceRecord info = Assert.Single(scope.TracesOfType("OpenTelemetryMetricsFactory"), t => t.Message.Contains("completed for every Geneva collector again"));
            Assert.Equal(MetricsTraceLevel.Info, info.Level);
            Assert.Contains("after 2 incomplete flush(es)", info.Message);
        }
    }
}
