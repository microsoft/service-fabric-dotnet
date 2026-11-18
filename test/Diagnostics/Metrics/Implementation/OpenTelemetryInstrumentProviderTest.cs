// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Tests the API behavior of meter providers on the OpenTelemetry backend, which must match the native implementation on
/// the COM interop path.
/// </summary>
public abstract class OpenTelemetryInstrumentProviderTest : IDisposable
{
    const int E_FAIL = unchecked((int)0x80004005);

    readonly MetricsTestScope scope = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    void IDisposable.Dispose() => ((IDisposable)scope).Dispose();

    public sealed class CreateInstrument : OpenTelemetryInstrumentProviderTest
    {
        readonly string metricNamespace = fuzzy.String();
        readonly string name = fuzzy.String();
        readonly string dimension1Name = fuzzy.String();
        readonly string dimension2Name = fuzzy.String();
        readonly string dimension3Name = fuzzy.String();

        [Theory, InlineData(true), InlineData(false)]
        public void ThrowsArgumentExceptionForNullOrEmptyNamespaceOrName(bool gateOpen)
        {
            SetGate(gateOpen);
            using Int64MeterProvider provider = new();

            Assert.Equal(nameof(metricNamespace), Assert.Throws<ArgumentException>(() => provider.CreateMeter(null, name)).ParamName);
            Assert.Equal(nameof(metricNamespace), Assert.Throws<ArgumentException>(() => provider.CreateMeter(string.Empty, name)).ParamName);
            Assert.Equal(nameof(name), Assert.Throws<ArgumentException>(() => provider.CreateMeter(metricNamespace, null)).ParamName);
            Assert.Equal(nameof(name), Assert.Throws<ArgumentException>(() => provider.CreateMeter(metricNamespace, string.Empty, dimension1Name)).ParamName);
        }

        [Fact]
        public void ThrowsComExceptionWithEFailForPeriodNamespaceOrName()
        {
            using Int64MeterProvider provider = new();

            Assert.Equal(E_FAIL, Assert.Throws<COMException>(() => provider.CreateMeter(".", name)).HResult);
            Assert.Equal(E_FAIL, Assert.Throws<COMException>(() => provider.CreateMeter(metricNamespace, ".", dimension1Name)).HResult);
        }

        [Fact]
        public void ThrowsComExceptionWithEFailForNullOrEmptyDimensionName()
        {
            using TimeSpanMeterProvider provider = new();

            Assert.Equal(E_FAIL, Assert.Throws<COMException>(() => provider.CreateMeter(metricNamespace, name, string.Empty)).HResult);
            Assert.Equal(E_FAIL, Assert.Throws<COMException>(() => provider.CreateMeter(metricNamespace, name, dimension1Name, null)).HResult);
            Assert.Equal(E_FAIL, Assert.Throws<COMException>(() => provider.CreateMeter(metricNamespace, name, dimension1Name, dimension2Name, string.Empty)).HResult);
        }

        [Fact]
        public void AcceptsNamesAndDimensionsNativeCodeWouldRejectWhenGateIsClosed()
        {
            SetGate(gateOpen: false);
            using (Int64MeterProvider provider = new())
            {
                provider.CreateMeter(".", name).Record(fuzzy.Int64());
                provider.CreateMeter(metricNamespace, ".").Record(fuzzy.Int64());
                provider.CreateMeter(metricNamespace, name, string.Empty).Record(fuzzy.Int64(), fuzzy.String());
                provider.CreateMeter(metricNamespace, name, dimension1Name, null).Record(fuzzy.Int64(), fuzzy.String(), fuzzy.String());
            }

            // A closed gate builds no exporter.
            Assert.Empty(scope.Exporters);
        }

        [Theory, InlineData(true), InlineData(false)]
        public void ThrowsObjectDisposedExceptionBeforeValidatingArgumentsAfterDispose(bool gateOpen)
        {
            SetGate(gateOpen);
            Int64MeterProvider provider = new();
            provider.Dispose();

            var exception = Assert.Throws<ObjectDisposedException>(() => provider.CreateMeter(metricNamespace, name));
            Assert.Equal(nameof(MeterProvider<>), exception.ObjectName);
            _ = Assert.Throws<ObjectDisposedException>(() => provider.CreateMeter(metricNamespace, name, dimension1Name, dimension2Name, dimension3Name));

            // The disposed check precedes argument validation, as in the COM implementation.
            _ = Assert.Throws<ObjectDisposedException>(() => provider.CreateMeter(null, null));
        }
    }

    public sealed class Dispose : OpenTelemetryInstrumentProviderTest
    {
        [Fact]
        public void ExportsEverythingRecordedSoFar()
        {
            string name = MetricsTestScope.UniqueName("DisposeFlush");
            // Negative values are clamped to 0.
            long value = fuzzy.Int64().Minimum(0);
            Int64MeterProvider provider = new();
            provider.CreateMeter("Namespace", name).Record(value);
            int exportsBefore = scope.Exporter.ExportCount;

            provider.Dispose();

            Assert.Equal(exportsBefore + 1, scope.Exporter.ExportCount);
            Assert.Equal(value, scope.Exporter.Points.Single(p => p.MetricName == "Namespace." + name).Sum);
        }

        [Fact]
        public void IsIdempotentAndKeepsInstrumentsWorking()
        {
            string name = MetricsTestScope.UniqueName("AfterDispose");
            Int64MeterProvider provider = new();
            IMeter1D<long> meter = provider.CreateMeter("Namespace", name, fuzzy.String());

            provider.Dispose();
            provider.Dispose();
            meter.Record(fuzzy.Int64(), fuzzy.String());

            Assert.Equal(1, scope.FlushAndGet("Namespace." + name)[0].Count);
        }
    }

    void SetGate(bool gateOpen) => _ = scope.Source.Set("Telemetry/Metrics", "IsEnabled", gateOpen ? "true" : "false");
}
