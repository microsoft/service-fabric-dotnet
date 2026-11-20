// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Tests the process-wide state of the OpenTelemetry backend: the configuration gate, collectors and the SDK provider lifetime.
/// </summary>
public abstract class SfMetricsPipelineTest : IDisposable
{
    readonly MetricsTestScope scope = new();

    void IDisposable.Dispose() => ((IDisposable)scope).Dispose();

    public sealed class ForceFlush : SfMetricsPipelineTest
    {
        [Fact]
        public void TracesWarningWhenFlushingThrows()
        {
            SfMetricsPipeline.FlushSdkProvider = _ => throw new InvalidOperationException();
            using Int64MeterProvider provider = new();

            SfMetricsPipeline.ForceFlush();

            Assert.Contains(
                scope.TracesOfType("OpenTelemetryMetricsFactory"),
                t => t.Level == MetricsTraceLevel.Warning && t.Message.StartsWith("Failed to flush managed metrics.", StringComparison.Ordinal));
        }
    }

    public sealed class Start : SfMetricsPipelineTest
    {
        [Theory]
        [InlineData("IsEnabled", "false")]
        [InlineData("IsManagedTelemetryEnabled", "FALSE")]
        [InlineData("IsEnabled", "yes")] // Native config parses anything but "true" as false.
        public void DisablesMetricsAndBuildsNoExporterUnlessGateSettingsAreTrue(string key, string value)
        {
            _ = scope.Source.Set("Telemetry/Metrics", key, value);

            using (Int64MeterProvider provider = new())
            {
                provider.CreateMeter(".", "Name").Record(1);
                provider.CreateMeter("Namespace", "Name", "D1").Record(1, "a");
            }

            Assert.False(SfMetricsPipeline.MetricsEnabled);
            Assert.Empty(scope.Exporters);
        }

        [Fact]
        public void EnablesMetricsWhenGateSettingsAreTrueIgnoringCase()
        {
            _ = scope.Source.Set("Telemetry/Metrics", "IsEnabled", "TRUE").Set("Telemetry/Metrics", "IsManagedTelemetryEnabled", "True");
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("GateOpen");

            provider.CreateMeter("Namespace", name).Record(1);

            _ = Assert.Single(scope.FlushAndGet("Namespace." + name));
        }

        [Fact]
        public void ValidatesAndRecordsButBuildsNoExporterWithoutCollector()
        {
            _ = scope.Source.SetCollectors();
            Int64MeterProvider provider = new();

            _ = Assert.Throws<COMException>(() => provider.CreateMeter(".", "Name"));
            provider.CreateMeter("Namespace", MetricsTestScope.UniqueName("NoCollector")).Record(1);
            provider.Dispose();

            Assert.True(SfMetricsPipeline.MetricsEnabled);
            Assert.Empty(scope.Exporters);
        }

        [Fact]
        public void ReadsSettingsOnce()
        {
            int loads = 0;
            SfMetricsPipeline.LoadSettings = () =>
            {
                loads++;
                return MetricsSettings.Load(scope.Source);
            };

            using (new Int64MeterProvider())
            using (new TimeSpanMeterProvider())
            using (new Int64MeterProvider())
            {
                Assert.Equal(1, loads);
                _ = Assert.Single(scope.Exporters);
            }
        }

        [Fact]
        public void TracesHowLongBuildingTheSdkProviderTook()
        {
            using Int64MeterProvider provider = new();

            Assert.Contains(
                scope.TracesOfType("OpenTelemetryMetricsFactory"),
                t => t.Level == MetricsTraceLevel.Info && t.Message.StartsWith("Building the OpenTelemetry SDK provider took ", StringComparison.Ordinal));
        }

        [Fact]
        public void SuppliesOpenTelemetryOnceBeforeAnythingLoadsIt()
        {
            List<string> steps = [];
            SfMetricsPipeline.SupplyOpenTelemetry = () => steps.Add("supply");
            Action loadDiagnosticSource = SfMetricsPipeline.LoadDiagnosticSource;
            SfMetricsPipeline.LoadDiagnosticSource = () =>
            {
                steps.Add("load DiagnosticSource");
                loadDiagnosticSource();
            };
            Action loadGenevaExporter = SfOpenTelemetrySdk.LoadGenevaExporter;
            SfOpenTelemetrySdk.LoadGenevaExporter = () =>
            {
                steps.Add("load Geneva exporter");
                loadGenevaExporter();
            };
            Func<MetricsSettings, IDisposable> buildSdkProvider = SfMetricsPipeline.BuildSdkProvider;
            SfMetricsPipeline.BuildSdkProvider = settings =>
            {
                steps.Add("build");
                return buildSdkProvider(settings);
            };

            using (new Int64MeterProvider())
            using (new TimeSpanMeterProvider())
            {
                Assert.Equal(["supply", "load DiagnosticSource", "load Geneva exporter", "build"], steps);
            }
        }

        [Fact]
        public void DoesNotSupplyOpenTelemetryWhenMetricsAreDisabled()
        {
            int supplies = 0;
            SfMetricsPipeline.SupplyOpenTelemetry = () => supplies++;
            _ = scope.Source.Set("Telemetry/Metrics", "IsEnabled", "false");

            using Int64MeterProvider provider = new();

            Assert.Equal(0, supplies);
        }

        [Theory]
        [InlineData("load DiagnosticSource")]
        [InlineData("load Geneva exporter")]
        [InlineData("build")]
        public void StopsResolvingOpenTelemetryWhenStartFails(string failingStep)
        {
            string[] order = ["load DiagnosticSource", "load Geneva exporter", "build"];
            List<string> steps = [];
            SfMetricsPipeline.SupplyOpenTelemetry = () => steps.Add("supply");
            SfMetricsPipeline.WithdrawOpenTelemetry = () => steps.Add("withdraw");
            SfMetricsPipeline.LoadDiagnosticSource = () => Step("load DiagnosticSource");
            SfOpenTelemetrySdk.LoadGenevaExporter = () => Step("load Geneva exporter");
            SfMetricsPipeline.BuildSdkProvider = _ =>
            {
                Step("build");
                return null;
            };

            InvalidOperationException e = Assert.Throws<InvalidOperationException>(SfMetricsPipeline.Start);

            Assert.Equal(failingStep, e.Message);
            Assert.Equal(["supply", .. order.TakeWhile(step => step != failingStep), failingStep, "withdraw"], steps);
            Assert.False(SfMetricsPipeline.MetricsEnabled);

            void Step(string step)
            {
                steps.Add(step);
                if (step == failingStep)
                    throw new InvalidOperationException(step);
            }
        }

        [Fact]
        public void KeepsResolvingOpenTelemetryWhenStartSucceeds()
        {
            int withdrawals = 0;
            SfMetricsPipeline.WithdrawOpenTelemetry = () => withdrawals++;

            using Int64MeterProvider provider = new();

            Assert.True(SfMetricsPipeline.MetricsEnabled);
            Assert.Equal(0, withdrawals);
        }
    }
}
