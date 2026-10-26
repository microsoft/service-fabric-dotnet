// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// API behavior of meter providers on the OpenTelemetry backend, which must match the native implementation on the COM interop
/// path.
/// </summary>
[Collection(MetricsPipelineCollection.Name)]
public abstract class OpenTelemetryInstrumentProviderTest : IDisposable
{
    const int E_FAIL = unchecked((int)0x80004005);

    readonly MetricsTestScope scope = new();

    void IDisposable.Dispose() => scope.Dispose();

    public sealed class CreateInstrument : OpenTelemetryInstrumentProviderTest
    {
        [Theory, InlineData(true), InlineData(false)]
        public void ThrowsArgumentExceptionForNullOrEmptyNamespaceOrName(bool gateOpen)
        {
            SetGate(gateOpen);
            using Int64MeterProvider provider = new();

            _ = Assert.Throws<ArgumentException>(() => provider.CreateMeter(null, "Name"));
            _ = Assert.Throws<ArgumentException>(() => provider.CreateMeter(string.Empty, "Name"));
            _ = Assert.Throws<ArgumentException>(() => provider.CreateMeter("Namespace", null));
            _ = Assert.Throws<ArgumentException>(() => provider.CreateMeter("Namespace", string.Empty, "Dimension1"));
        }

        [Fact]
        public void ThrowsComExceptionWithEFailForPeriodNamespaceOrName()
        {
            using Int64MeterProvider provider = new();

            Assert.Equal(E_FAIL, Assert.Throws<COMException>(() => provider.CreateMeter(".", "Name")).HResult);
            Assert.Equal(E_FAIL, Assert.Throws<COMException>(() => provider.CreateMeter("Namespace", ".", "Dimension1")).HResult);
        }

        [Fact]
        public void ThrowsComExceptionForNullOrEmptyDimensionName()
        {
            using TimeSpanMeterProvider provider = new();

            _ = Assert.Throws<COMException>(() => provider.CreateMeter("Namespace", "Name", string.Empty));
            _ = Assert.Throws<COMException>(() => provider.CreateMeter("Namespace", "Name", "Dimension1", null));
            _ = Assert.Throws<COMException>(() => provider.CreateMeter("Namespace", "Name", "Dimension1", "Dimension2", string.Empty));
        }

        [Fact]
        public void AcceptsNamesAndDimensionsNativeCodeWouldRejectWhenGateIsClosed()
        {
            SetGate(gateOpen: false);
            using (Int64MeterProvider provider = new())
            {
                provider.CreateMeter(".", "Name").Record(1);
                provider.CreateMeter("Namespace", ".").Record(1);
                provider.CreateMeter("Namespace", "Name", string.Empty).Record(1, "value");
                provider.CreateMeter("Namespace", "Name", "Dimension1", null).Record(1, "value1", "value2");
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

            _ = Assert.Throws<ObjectDisposedException>(() => provider.CreateMeter("Namespace", "Name"));
            _ = Assert.Throws<ObjectDisposedException>(() => provider.CreateMeter("Namespace", "Name", "Dimension1", "Dimension2", "Dimension3"));

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
            Int64MeterProvider provider = new();
            provider.CreateMeter("Namespace", name).Record(10);
            int exportsBefore = scope.Exporter.ExportCount;

            provider.Dispose();

            Assert.Equal(exportsBefore + 1, scope.Exporter.ExportCount);
            Assert.Equal(10d, scope.Exporter.Points.Single(p => p.MetricName == "Namespace." + name).Sum);
        }

        [Fact]
        public void IsIdempotentAndKeepsInstrumentsWorking()
        {
            string name = MetricsTestScope.UniqueName("AfterDispose");
            Int64MeterProvider provider = new();
            IMeter1D<long> meter = provider.CreateMeter("Namespace", name, "D1");

            provider.Dispose();
            provider.Dispose();
            meter.Record(5, "a");

            Assert.Equal(1, scope.FlushAndGet("Namespace." + name)[0].Count);
        }
    }

    void SetGate(bool gateOpen) => _ = scope.Source.Set("Telemetry/Metrics", "IsEnabled", gateOpen ? "true" : "false");
}
