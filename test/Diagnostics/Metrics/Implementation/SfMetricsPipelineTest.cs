// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Process-wide state of the OpenTelemetry backend: the configuration gate, collectors and the SDK provider lifetime.
/// </summary>
[Collection(MetricsPipelineCollection.Name)]
public abstract class SfMetricsPipelineTest : IDisposable
{
    readonly MetricsTestScope scope = new();

    void IDisposable.Dispose() => scope.Dispose();

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
    }
}
