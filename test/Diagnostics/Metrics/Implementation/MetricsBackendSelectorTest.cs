// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Fuzzy;
using Inspector;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

[Collection(MetricsPipelineCollection.Name)]
public abstract class MetricsBackendSelectorTest : IDisposable
{
    const string TraceType = "ManagedMetricsBackend";

    readonly MetricsTestScope scope = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    void IDisposable.Dispose() => scope.Dispose();

    public sealed class Backend : MetricsBackendSelectorTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void IsComInteropWhenSettingIsNotSet(string value)
        {
            SetBackend(value);

            Assert.Equal(MetricsBackend.ComInterop, MetricsBackendSelector.Backend);
            _ = AssertInfoTrace(MetricsBackend.ComInterop);
        }

        [Theory]
        [InlineData("ComInterop")]
        [InlineData("cominterop")]
        public void IsComInteropWhenSettingIsComInteropIgnoringCase(string value)
        {
            SetBackend(value);

            Assert.Equal(MetricsBackend.ComInterop, MetricsBackendSelector.Backend);
            _ = AssertInfoTrace(MetricsBackend.ComInterop);
            Assert.False(SfMetricsPipeline.MetricsEnabled);
        }

        [Theory]
        [InlineData("OpenTelemetry")]
        [InlineData("OPENTELEMETRY")]
        public void IsOpenTelemetryWithStartedPipelineWhenSettingIsOpenTelemetryIgnoringCase(string value)
        {
            SetBackend(value);

            Assert.Equal(MetricsBackend.OpenTelemetry, MetricsBackendSelector.Backend);
            _ = AssertInfoTrace(MetricsBackend.OpenTelemetry);
            Assert.True(SfMetricsPipeline.MetricsEnabled);
            _ = Assert.Single(scope.Exporters);
        }

        [Fact]
        public void IsComInteropAndTracesWarningWhenSettingHasUnknownValue()
        {
            string value = fuzzy.String();
            SetBackend(value);

            Assert.Equal(MetricsBackend.ComInterop, MetricsBackendSelector.Backend);
            Assert.Contains(value, AssertWarningTrace().Message);
            _ = AssertInfoTrace(MetricsBackend.ComInterop);
        }

        [Fact]
        public void IsComInteropWhenSettingCannotBeRead()
        {
            DllNotFoundException error = new(fuzzy.String());
            IMetricsSettingsSource source = Mock.Of<IMetricsSettingsSource>();
            _ = Mock.Get(source).Setup(_ => _.ReadConfigValue("Telemetry/Metrics", "ManagedMetricsBackend")).Throws(error);
            MetricsBackendSelector.CreateSettingsSource = () => source;

            Assert.Equal(MetricsBackend.ComInterop, MetricsBackendSelector.Backend);
            Assert.Contains(error.Message, AssertInfoTrace(MetricsBackend.ComInterop).Message);
            Assert.DoesNotContain(scope.Traces, t => t.Level == MetricsTraceLevel.Warning);
        }

        [Fact]
        public void IsComInteropAndTracesWarningWhenOpenTelemetrySettingsCannotBeLoaded()
        {
            InvalidOperationException error = new(fuzzy.String());
            SfMetricsPipeline.LoadSettings = () => throw error;

            Assert.Equal(MetricsBackend.ComInterop, MetricsBackendSelector.Backend);
            Assert.Contains(error.Message, AssertWarningTrace().Message);
            _ = AssertInfoTrace(MetricsBackend.ComInterop);
            Assert.False(SfMetricsPipeline.MetricsEnabled);
        }

        [Fact]
        public void IsComInteropAndTracesWarningWhenOpenTelemetrySdkProviderCannotBeBuilt()
        {
            InvalidOperationException error = new(fuzzy.String());
            SfMetricsPipeline.BuildSdkProvider = _ => throw error;

            Assert.Equal(MetricsBackend.ComInterop, MetricsBackendSelector.Backend);
            Assert.Contains(error.Message, AssertWarningTrace().Message);
            _ = AssertInfoTrace(MetricsBackend.ComInterop);
            Assert.False(SfMetricsPipeline.MetricsEnabled);
        }

        [Fact]
        public void IsComInteropAndTracesWarningWhenLoadedOpenTelemetryIsOlderThanGenevaExporterRequires()
        {
            SfOpenTelemetrySdk.ReadFileVersion = _ => new Version(1, 9, 0, 1312);

            Assert.Equal(MetricsBackend.ComInterop, MetricsBackendSelector.Backend);
            Assert.Contains("1.9.0.1312", AssertWarningTrace().Message);
            _ = AssertInfoTrace(MetricsBackend.ComInterop);
            Assert.False(SfMetricsPipeline.MetricsEnabled);
            Assert.Empty(scope.Exporters);
        }

        [Fact]
        public void ReadsSettingOnce()
        {
            int reads = 0;
            MetricsBackendSelector.CreateSettingsSource = () =>
            {
                reads++;
                return scope.Source;
            };

            MetricsBackend backend = MetricsBackendSelector.Backend;

            Assert.Equal(backend, MetricsBackendSelector.Backend);
            Assert.Equal(1, reads);
            _ = AssertInfoTrace(backend);
        }
    }

    public sealed class CreateInstrumentProvider : MetricsBackendSelectorTest
    {
        readonly IReadOnlyCollection<string> fixedDimensionNames = fuzzy.List(fuzzy.String);
        readonly IReadOnlyCollection<string> fixedDimensionValues;

        public CreateInstrumentProvider() =>
            fixedDimensionValues = fuzzy.List(fuzzy.String, Count.Exactly(fixedDimensionNames.Count));

        [Fact]
        public void ReturnsOpenTelemetryInstrumentProviderWithFixedDimensionsWhenBackendIsOpenTelemetry()
        {
            using IInstrumentProvider provider = MetricsBackendSelector.CreateInstrumentProvider(fixedDimensionNames, fixedDimensionValues);

            KeyValuePair<string, object>[] fixedTags = ((OpenTelemetryInstrumentProvider)provider).Field<KeyValuePair<string, object>[]>().Value;
            Assert.Equal(fixedDimensionNames, fixedTags.Select(t => t.Key));
            Assert.Equal(fixedDimensionValues, fixedTags.Select(t => t.Value.ToString()));
        }

        [Fact]
        public void ReturnsComInstrumentProviderWithFixedDimensionsWhenBackendIsComInterop()
        {
            SetBackend(nameof(MetricsBackend.ComInterop));
            Func<IFabricMeterProvider2> createFabricMeterProvider = typeof(ComInstrumentProvider).Field<Func<IFabricMeterProvider2>>().Value;
            typeof(ComInstrumentProvider).Field<Func<IFabricMeterProvider2>>().Set(() => Mock.Of<IFabricMeterProvider2>());
            try
            {
                var provider = (ComInstrumentProvider)MetricsBackendSelector.CreateInstrumentProvider(fixedDimensionNames, fixedDimensionValues);

                Assert.Same(fixedDimensionNames, provider.Field<IReadOnlyCollection<string>>("fixedDimensionNames").Value);
                Assert.Same(fixedDimensionValues, provider.Field<IReadOnlyCollection<string>>("fixedDimensionValues").Value);
            }
            finally
            {
                typeof(ComInstrumentProvider).Field<Func<IFabricMeterProvider2>>().Set(createFabricMeterProvider);
            }
        }
    }

    void SetBackend(string value) => _ = scope.Source.Set("Telemetry/Metrics", "ManagedMetricsBackend", value);

    TraceRecord AssertInfoTrace(MetricsBackend backend)
    {
        TraceRecord trace = Assert.Single(scope.TracesOfType(TraceType), t => t.Level == MetricsTraceLevel.Info);
        Assert.Contains($"the {backend} metrics backend", trace.Message);
        return trace;
    }

    TraceRecord AssertWarningTrace() =>
        Assert.Single(scope.TracesOfType(TraceType), t => t.Level == MetricsTraceLevel.Warning);
}
