// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Tests the API behavior of meters on the OpenTelemetry backend, which must match the native implementation on the COM
/// interop path.
/// </summary>
public abstract class OpenTelemetryInstrumentTest : IDisposable
{
    readonly MetricsTestScope scope = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    void IDisposable.Dispose() => ((IDisposable)scope).Dispose();

    public sealed class Record : OpenTelemetryInstrumentTest
    {
        readonly long value = fuzzy.Int64();
        readonly string dimension1Value = fuzzy.String();
        readonly string dimension2Value = fuzzy.String();
        readonly string dimension3Value = fuzzy.String();

        [Fact]
        public void ThrowsArgumentNullExceptionForNullDimensionValueBeforeCheckingDisposal()
        {
            using Int64MeterProvider provider = new();
            IMeter1D<long> meter1 = provider.CreateMeter("Namespace", MetricsTestScope.UniqueName("Null"), fuzzy.String());
            IMeter2D<long> meter2 = provider.CreateMeter("Namespace", MetricsTestScope.UniqueName("Null"), fuzzy.String(), fuzzy.String());
            IMeter3D<long> meter3 = provider.CreateMeter("Namespace", MetricsTestScope.UniqueName("Null"), fuzzy.String(), fuzzy.String(), fuzzy.String());

            Assert.Equal(nameof(dimension1Value), Assert.Throws<ArgumentNullException>(() => meter1.Record(value, null)).ParamName);
            Assert.Equal(nameof(dimension2Value), Assert.Throws<ArgumentNullException>(() => meter2.Record(value, dimension1Value, null)).ParamName);
            Assert.Equal(nameof(dimension1Value), Assert.Throws<ArgumentNullException>(() => meter3.Record(value, null, dimension2Value, dimension3Value)).ParamName);

            meter1.Dispose();
            meter3.Dispose();
            Assert.Equal(nameof(dimension1Value), Assert.Throws<ArgumentNullException>(() => meter1.Record(value, null)).ParamName);
            Assert.Equal(nameof(dimension3Value), Assert.Throws<ArgumentNullException>(() => meter3.Record(value, dimension1Value, dimension2Value, null)).ParamName);
        }

        [Theory, InlineData(true), InlineData(false)]
        public void ThrowsObjectDisposedExceptionAfterDispose(bool gateOpen)
        {
            _ = scope.Source.Set("Telemetry/Metrics", "IsEnabled", gateOpen ? "true" : "false");
            using Int64MeterProvider longProvider = new();
            using TimeSpanMeterProvider timeSpanProvider = new();
            string name = MetricsTestScope.UniqueName("Disposed");
            IMeter<long> meter0 = longProvider.CreateMeter("Namespace", name);
            IMeter1D<long> meter1 = longProvider.CreateMeter("Namespace", name, fuzzy.String());
            IMeter2D<TimeSpan> meter2 = timeSpanProvider.CreateMeter("Namespace", name, fuzzy.String(), fuzzy.String());
            IMeter3D<TimeSpan> meter3 = timeSpanProvider.CreateMeter("Namespace", name, fuzzy.String(), fuzzy.String(), fuzzy.String());

            meter0.Dispose();
            meter1.Dispose();
            meter2.Dispose();
            meter3.Dispose();
            meter3.Dispose();

            var exception = Assert.Throws<ObjectDisposedException>(() => meter0.Record(value));
            Assert.Equal(nameof(Meter), exception.ObjectName);
            _ = Assert.Throws<ObjectDisposedException>(() => meter1.Record(value, dimension1Value));
            _ = Assert.Throws<ObjectDisposedException>(() => meter2.Record(fuzzy.TimeSpan(), dimension1Value, dimension2Value));
            _ = Assert.Throws<ObjectDisposedException>(() => meter3.Record(fuzzy.TimeSpan(), dimension1Value, dimension2Value, dimension3Value));
        }

        [Fact]
        public void ClampsNegativeValueToZeroAndTracesFirstAndEvery1000thOccurrence()
        {
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Negative");
            IMeter1D<long> meter = provider.CreateMeter("Namespace", name, fuzzy.String());

            for (int i = 0; i < 1000; i++)
                meter.Record(-5, dimension1Value);

            ExportedPoint point = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Equal(1000, point.Count);
            Assert.Equal(0d, point.Sum);
            Assert.Equal(0d, point.Max);
            IReadOnlyList<TraceRecord> traces = scope.TracesOfType("FabricMeter");
            Assert.Equal(2, traces.Count);
            Assert.Contains("Negative value -5 clamped to 0", traces[0].Message);
            Assert.Contains("occurrence 1000", traces[1].Message);
        }

        [Fact]
        public void ClampsAndTracesNegativeValueWhenGateIsClosed()
        {
            _ = scope.Source.Set("Telemetry/Metrics", "IsEnabled", "false");

            using (Int64MeterProvider provider = new())
                provider.CreateMeter(fuzzy.String(), fuzzy.String()).Record(-1);

            _ = Assert.Single(scope.TracesOfType("FabricMeter"));
        }

        [Fact]
        public void EitherRecordsOrThrowsObjectDisposedExceptionWhenConcurrentWithDispose()
        {
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("ConcurrentMeterDispose");
            IMeter1D<long> meter = provider.CreateMeter("Namespace", name, fuzzy.String());
            ConcurrentQueue<Exception> errors = new();
            long recorded = 0;
            int sawDisposed = 0;

            Thread[] recorders = RecorderThreads.Start(8, errors, () =>
            {
                try
                {
                    for (int i = 0; i < 10000000; i++)
                    {
                        meter.Record(1, dimension1Value);
                        _ = Interlocked.Increment(ref recorded);
                    }
                }
                catch (ObjectDisposedException)
                {
                    _ = Interlocked.Increment(ref sawDisposed);
                }
            });

            bool progressed = SpinWait.SpinUntil(() => Interlocked.Read(ref recorded) >= 10000, TimeSpan.FromSeconds(30));
            meter.Dispose();
            RecorderThreads.JoinAll(recorders);

            Assert.Empty(errors);
            Assert.True(progressed, "The recorders made no progress before the meter was disposed.");
            Assert.Equal(8, sawDisposed);

            // A Record either completed before the dispose or threw; none is half-recorded.
            Assert.Equal(Interlocked.Read(ref recorded), scope.FlushAndGet("Namespace." + name).Sum(p => p.Count));
        }
    }
}
