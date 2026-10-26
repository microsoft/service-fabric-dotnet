// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using DiagnosticsMeter = System.Diagnostics.Metrics.Meter;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

[Collection(MetricsPipelineCollection.Name)]
public abstract class SfOpenTelemetrySdkTest : IDisposable
{
    readonly MetricsTestScope scope = new();

    void IDisposable.Dispose() => scope.Dispose();

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

        [Fact]
        public void SkipsCollectorRejectedByExporter()
        {
            string rejected = MetricsTestScope.ValidConnectionString.Replace("TestAccount", "Rejected");
            _ = scope.Source.SetCollectors(rejected, MetricsTestScope.ValidConnectionString);
            var createTestExporter = SfOpenTelemetrySdk.CreateExporter;
            SfOpenTelemetrySdk.CreateExporter = (connectionString, dimensions) =>
                connectionString == rejected ? throw new ArgumentException("rejected by the exporter") : createTestExporter(connectionString, dimensions);
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Rejected");

            provider.CreateMeter("Namespace", name).Record(1);

            _ = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Contains(scope.TracesOfType("OpenTelemetryMetricsFactory"), t => t.Message.Contains("rejected by the exporter"));
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
}
