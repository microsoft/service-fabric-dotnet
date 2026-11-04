// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// What recorded values look like once exported: values, buckets, tags, aggregation and failure handling.
/// </summary>
[Collection(MetricsPipelineCollection.Name)]
public abstract class SfHistogramInstrumentTest : IDisposable
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    readonly MetricsTestScope scope = new();
    readonly StatefulServiceContext context = fuzzy.StatefulServiceContext();

    void IDisposable.Dispose() => scope.Dispose();

    public sealed class Record : SfHistogramInstrumentTest
    {
        // Default explicit bounds of the OpenTelemetry SDK, the same as the native implementation's.
        static readonly double[] defaultBounds = [0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000, double.PositiveInfinity];

        [Fact]
        public void ExportsHistogramWithCountSumMinMaxOnServiceFabricMeter()
        {
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Values");
            IMeter<long> meter = provider.CreateMeter("Namespace", name);

            meter.Record(1);
            meter.Record(2);
            meter.Record(30);

            ExportedPoint point = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Equal("Microsoft.ServiceFabric", point.MeterName);
            Assert.Equal("1.0", point.MeterVersion);
            Assert.Equal(3, point.Count);
            Assert.Equal(33d, point.Sum);
            Assert.True(point.HasMinMax);
            Assert.Equal(1d, point.Min);
            Assert.Equal(30d, point.Max);
            Assert.Empty(point.Tags);
        }

        [Fact]
        public void UsesDefaultBucketsAndCountsValuesAbove10000InOverflowBucket()
        {
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Buckets");
            IMeter<long> meter = provider.CreateMeter("Namespace", name);

            foreach (long value in new long[] { 0, 5, 6, 10000, 10001, long.MaxValue })
                meter.Record(value);

            ExportedPoint point = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Equal(defaultBounds, point.Buckets.Select(b => b.Key));
            Assert.Equal(1, point.BucketCount(0));
            Assert.Equal(1, point.BucketCount(5));
            Assert.Equal(1, point.BucketCount(10));
            Assert.Equal(1, point.BucketCount(10000));
            Assert.Equal(2, point.BucketCount(double.PositiveInfinity));
            Assert.Equal((double)long.MaxValue, point.Max);
        }

        [Fact]
        public void RoundsTimeSpanTotalMillisecondsToEvenLikeComInterop()
        {
            using TimeSpanMeterProvider provider = new();
            Dictionary<double, long> cases = new()
            {
                { 0.4, 0 },
                { 0.5, 0 },
                { 1.5, 2 },
                { 2.5, 2 },
                { 2.6, 3 },
                { 1234.0, 1234 },
                { -1.6, 0 },
            };

            foreach (KeyValuePair<double, long> c in cases)
            {
                string name = MetricsTestScope.UniqueName("TimeSpan");
                provider.CreateMeter("Namespace", name).Record(TimeSpan.FromTicks((long)(c.Key * TimeSpan.TicksPerMillisecond)));
                Assert.Equal((double)c.Value, Assert.Single(scope.FlushAndGet("Namespace." + name)).Sum);
            }
        }

        [Fact]
        public void AddsFixedDimensionsOfServiceContextFollowedByVariableDimensions()
        {
            using Int64MeterProvider provider = new(context);
            string name = MetricsTestScope.UniqueName("Tags");

            provider.CreateMeter("Namespace", name, "Mode", "Reason", "Kind").Record(7, "Read", "Timeout", "x");

            ExportedPoint point = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Equal(8, point.Tags.Count);
            AssertFixedDimensions(point);
            Assert.Equal("Read", point.Tag("Mode"));
            Assert.Equal("Timeout", point.Tag("Reason"));
            Assert.Equal("x", point.Tag("Kind"));
        }

        [Fact]
        public void ExportsFixedDimensionsOfMeterWithoutVariableDimensions()
        {
            using Int64MeterProvider provider = new(context);
            string name = MetricsTestScope.UniqueName("FixedOnly");
            IMeter<long> meter = provider.CreateMeter("Namespace", name);

            meter.Record(3);
            meter.Record(4);

            ExportedPoint point = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Equal(2, point.Count);
            Assert.Equal(7d, point.Sum);
            Assert.Equal(5, point.Tags.Count);
            AssertFixedDimensions(point);
        }

        [Fact]
        public void ExportsEachCombinationUnderItsOwnTagsBeyondTheTagCache()
        {
            using Int64MeterProvider provider = new(context);
            string name = MetricsTestScope.UniqueName("TagCache");
            IMeter2D<long> meter = provider.CreateMeter("Namespace", name, "D1", "D2");
            int combinations = 3 * SfHistogramInstrument.MaxCachedTagSets;

            // New string instances every round: the first round fills the cache, the second hits it by value, and
            // combinations past its capacity take the per-record TagList path.
            for (int round = 0; round < 2; round++)
                for (int i = 0; i < combinations; i++)
                    meter.Record(i, "v" + i, i % 2 == 0 ? "even" : "odd");

            IReadOnlyList<ExportedPoint> points = scope.FlushAndGet("Namespace." + name);
            Assert.Equal(combinations, points.Count);
            for (int i = 0; i < combinations; i++)
            {
                ExportedPoint point = points.Single(p => p.Tag("D1") == "v" + i);
                Assert.Equal(2, point.Count);
                Assert.Equal(2d * i, point.Sum);
                Assert.Equal(i % 2 == 0 ? "even" : "odd", point.Tag("D2"));
                Assert.Equal(7, point.Tags.Count);
                Assert.Equal(context.PartitionId.ToString(), point.Tag("PartitionId"));
            }
        }

        [Fact]
        public void ExportsEveryMeasurementUnderItsTagsOnConcurrentFirstUseOfDimensionValues()
        {
            using Int64MeterProvider provider = new(context);
            string name = MetricsTestScope.UniqueName("ConcurrentTags");
            IMeter1D<long> meter = provider.CreateMeter("Namespace", name, "D1");
            string[] values = [.. Enumerable.Range(0, 2 * SfHistogramInstrument.MaxCachedTagSets).Select(i => "v" + i)];
            const int Threads = 8;
            const int PerThread = 2000;
            ConcurrentQueue<Exception> errors = new();

            Thread[] recorders = RecorderThreads.Start(Threads, errors, () =>
            {
                for (int i = 0; i < PerThread; i++)
                    meter.Record(1, values[i % values.Length]);
            });
            RecorderThreads.JoinAll(recorders);

            Assert.Empty(errors);
            IReadOnlyList<ExportedPoint> points = scope.FlushAndGet("Namespace." + name);
            Assert.Equal(values.Length, points.Count);
            foreach (string value in values)
                Assert.Equal(Threads * PerThread / values.Length, points.Single(p => p.Tag("D1") == value).Count);
        }

        [Fact]
        public void HandsTheSdkTagValuesWithTheirHashOnBothPathsAndExportsTheStrings()
        {
            using Int64MeterProvider provider = new(context);
            string name = MetricsTestScope.UniqueName("HashedTags");
            IMeter2D<long> meter = provider.CreateMeter("Namespace", name, "D1", "D2");

            // The first combinations fill the tag cache; the last one takes the per-record path.
            for (int i = 0; i <= SfHistogramInstrument.MaxCachedTagSets; i++)
                meter.Record(1, "v" + i, "w");

            IReadOnlyList<ExportedPoint> points = scope.FlushAndGet("Namespace." + name);
            Assert.Equal(SfHistogramInstrument.MaxCachedTagSets + 1, points.Count);
            foreach (ExportedPoint point in points)
            {
                foreach (KeyValuePair<string, object> tag in point.Tags)
                    _ = Assert.IsType<SfTagValue>(tag.Value);

                AssertFixedDimensions(point);
                Assert.Equal("w", point.Tag("D2"));
            }
        }

        [Fact]
        public void RecordsSameCombinationAsOneSeriesWhetherCachedOrNot()
        {
            using Int64MeterProvider provider = new(context);
            string name = MetricsTestScope.UniqueName("SharedSeries");
            IMeter1D<long> fullCache = provider.CreateMeter("Namespace", name, "D1");
            IMeter1D<long> emptyCache = provider.CreateMeter("Namespace", name, "D1");

            // Fill the first meter's tag cache, so it records "x" on the per-record path while the second caches "x".
            for (int i = 0; i < SfHistogramInstrument.MaxCachedTagSets; i++)
                fullCache.Record(1, "c" + i);

            fullCache.Record(2, "x");
            emptyCache.Record(3, "x");

            ExportedPoint point = scope.FlushAndGet("Namespace." + name).Single(p => p.Tag("D1") == "x");
            Assert.Equal(2, point.Count);
            Assert.Equal(5d, point.Sum);
        }

        [Fact]
        public void ExportsEachValueUnderItsOwnTagsBeyondTheValuesThePerRecordPathKeeps()
        {
            using Int64MeterProvider provider = new(context);
            string name = MetricsTestScope.UniqueName("FallbackBound");
            IMeter1D<long> meter = provider.CreateMeter("Namespace", name, "D1");
            int values = SfHistogramInstrument.MaxCachedTagSets + SfHistogramInstrument.MaxFallbackTagValues + 50;

            for (int round = 0; round < 2; round++)
                for (int i = 0; i < values; i++)
                    meter.Record(i, "v" + i);

            Dictionary<string, ExportedPoint> points = scope.FlushAndGet("Namespace." + name).ToDictionary(p => p.Tag("D1"));
            Assert.Equal(values, points.Count);
            for (int i = 0; i < values; i++)
            {
                ExportedPoint point = points["v" + i];
                Assert.Equal(2, point.Count);
                Assert.Equal(2d * i, point.Sum);
            }
        }

        [Fact]
        public void ExportsEmptyDimensionValueAsEmptyString()
        {
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("EmptyValue");

            provider.CreateMeter("Namespace", name, "D1", "D2").Record(1, string.Empty, "b");

            ExportedPoint point = Assert.Single(scope.FlushAndGet("Namespace." + name));
            Assert.Equal(string.Empty, point.Tag("D1"));
            Assert.Equal("b", point.Tag("D2"));
        }

        [Fact]
        public void AggregatesSameNameFromTwoProvidersIntoOneSeries()
        {
            using Int64MeterProvider provider1 = new();
            using Int64MeterProvider provider2 = new();
            string name = MetricsTestScope.UniqueName("Shared");

            provider1.CreateMeter("Namespace", name, "D1").Record(1, "a");
            provider2.CreateMeter("Namespace", name, "D1").Record(2, "a");
            provider2.CreateMeter("Namespace", name, "D1").Record(4, "b");

            IReadOnlyList<ExportedPoint> points = scope.FlushAndGet("Namespace." + name);
            Assert.Equal(2, points.Count);
            ExportedPoint a = points.Single(p => p.Tag("D1") == "a");
            Assert.Equal(2, a.Count);
            Assert.Equal(3d, a.Sum);
        }

        [Fact]
        public void TracesListenerFailureWithoutPassingItToCaller()
        {
            using MeterListener throwingListener = new();

            // Subscribed before Service Fabric's SDK provider, so it runs first.
            throwingListener.InstrumentPublished = static (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, SfMeterRegistry.Meter))
                    listener.EnableMeasurementEvents(instrument);
            };
            throwingListener.SetMeasurementEventCallback<long>(static (instrument, value, tags, state) => throw new InvalidOperationException("listener failure"));
            throwingListener.Start();

            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Throwing");
            IMeter1D<long> meter = provider.CreateMeter("Namespace", name, "D1");

            meter.Record(1, "a");
            meter.Record(2, "a");

            // Traced on the first failure and then every 1000th.
            TraceRecord trace = Assert.Single(scope.TracesOfType("FabricTelemetry.Meter"));
            Assert.Contains("listener failure", trace.Message);

            // The throwing listener ran first, so Service Fabric's pipeline did not receive these measurements.
            Assert.Empty(scope.FlushAndGet("Namespace." + name));
        }

        [Fact]
        public void CountsAllRecordsFromManyThreads()
        {
            using Int64MeterProvider provider = new();
            string name = MetricsTestScope.UniqueName("Concurrent");
            IMeter2D<long> meter = provider.CreateMeter("Namespace", name, "D1", "D2");

            _ = Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, worker =>
            {
                for (int i = 0; i < 10000; i++)
                    meter.Record(1, "w" + (worker % 2), "x");
            });

            IReadOnlyList<ExportedPoint> points = scope.FlushAndGet("Namespace." + name);
            Assert.Equal(2, points.Count);
            Assert.Equal(80000, points.Sum(p => p.Count));
            Assert.Equal(80000d, points.Sum(p => p.Sum));
        }

        [Fact]
        public void ExportsRecordDuringInFlightExportOnceByTheNextExport()
        {
            using Int64MeterProvider provider = new();
            string metric = CreateRaceMeter(provider, "InFlightExport", out IMeter1D<long> meter);

            AssertRecordsDuringFlushAreExportedOnce(metric, meter, SfMetricsPipeline.ForceFlush);
        }

        [Fact]
        public void ExportsRecordDuringProviderDisposeOnce()
        {
            Int64MeterProvider provider = new();
            string metric = CreateRaceMeter(provider, "InFlightProviderDispose", out IMeter1D<long> meter);

            // Provider Dispose flushes; the meter keeps recording during and after it.
            AssertRecordsDuringFlushAreExportedOnce(metric, meter, provider.Dispose);
        }

        [Fact]
        public void ExportsEveryMeasurementExactlyOnceWhileExportsRunRepeatedly()
        {
            using Int64MeterProvider provider = new();
            string metric = CreateRaceMeter(provider, "RepeatedExports", out IMeter1D<long> meter);
            ConcurrentQueue<Exception> errors = new();
            long recorded = 0;
            int stop = 0;
            int overlappingExports = 0;

            Thread[] recorders = RecorderThreads.Start(8, errors, () =>
            {
                while (Volatile.Read(ref stop) == 0)
                {
                    meter.Record(1, "a");
                    _ = Interlocked.Increment(ref recorded);
                }
            });

            try
            {
                // Export until 20 exports overlapped recording on the other threads, or 30 seconds pass.
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                while (overlappingExports < 20 && elapsed.Elapsed < TimeSpan.FromSeconds(30) && errors.IsEmpty)
                {
                    long before = Interlocked.Read(ref recorded);
                    SfMetricsPipeline.ForceFlush();
                    if (Interlocked.Read(ref recorded) != before)
                        overlappingExports++;
                }
            }
            finally
            {
                Volatile.Write(ref stop, 1);
            }

            RecorderThreads.JoinAll(recorders);
            Assert.Empty(errors);
            Assert.SkipWhen(overlappingExports == 0, "No export overlapped recording on this machine.");
            Assert.Equal(Interlocked.Read(ref recorded), scope.FlushAndGet(metric).Sum(p => p.Count));
        }

#if NET
        [Fact]
        public void DoesNotAllocateForWarmedSeriesWithEightTags()
        {
            using Int64MeterProvider longProvider = new(context);
            using TimeSpanMeterProvider timeSpanProvider = new(context);

            // 5 fixed + 3 variable dimensions: the most tags a record carries, and TagList's inline capacity.
            string longName = MetricsTestScope.UniqueName("LongAllocations");
            string timeSpanName = MetricsTestScope.UniqueName("TimeSpanAllocations");
            IMeter3D<long> longMeter = longProvider.CreateMeter("Namespace", longName, "D1", "D2", "D3");
            IMeter3D<TimeSpan> timeSpanMeter = timeSpanProvider.CreateMeter("Namespace", timeSpanName, "D1", "D2", "D3");
            TimeSpan elapsed = TimeSpan.FromMilliseconds(12);
            for (int i = 0; i < 1000; i++)
            {
                longMeter.Record(i, "a", "b", "c");
                timeSpanMeter.Record(elapsed, "a", "b", "c");
            }

            // Measured in rounds: when the whole suite runs, work outside Record occasionally allocates once (a few
            // hundred bytes to a few KB) on this thread at a random point. A Record that allocates, even rarely,
            // shows up in every round.
            long[] allocatedPerRound = new long[3];
            for (int round = 0; round < allocatedPerRound.Length; round++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++)
                {
                    longMeter.Record(i, "a", "b", "c");
                    timeSpanMeter.Record(elapsed, "a", "b", "c");
                }

                allocatedPerRound[round] = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            Assert.True(allocatedPerRound.Min() == 0, $"A Record on a warmed series must not allocate. Bytes per round of 20,000 Records: {string.Join(", ", allocatedPerRound)}.");

            // Every record reached the SDK aggregation, so the measured path is the real one.
            Assert.Equal(31000, Assert.Single(scope.FlushAndGet("Namespace." + longName)).Count);
            Assert.Equal(31000, Assert.Single(scope.FlushAndGet("Namespace." + timeSpanName)).Count);
        }

        [Fact]
        public void DoesNotAllocateForWarmedSeriesBeyondTheTagCache()
        {
            using Int64MeterProvider provider = new(context);
            string name = MetricsTestScope.UniqueName("FallbackAllocations");
            IMeter3D<long> meter = provider.CreateMeter("Namespace", name, "D1", "D2", "D3");

            // Fill the instrument's tag cache, so the measured combination takes the per-record TagList path.
            for (int i = 0; i < SfHistogramInstrument.MaxCachedTagSets; i++)
                meter.Record(1, "c" + i, "b", "c");

            for (int i = 0; i < 1000; i++)
                meter.Record(i, "x", "y", "z");

            long[] allocatedPerRound = new long[3];
            for (int round = 0; round < allocatedPerRound.Length; round++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++)
                    meter.Record(i, "x", "y", "z");

                allocatedPerRound[round] = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            Assert.True(allocatedPerRound.Min() == 0, $"A Record past the tag cache must not allocate. Bytes per round of 10,000 Records: {string.Join(", ", allocatedPerRound)}.");
            Assert.Equal(31000, scope.FlushAndGet("Namespace." + name).Single(p => p.Tag("D1") == "x").Count);
        }
#endif

        static string CreateRaceMeter(Int64MeterProvider provider, string prefix, out IMeter1D<long> meter)
        {
            string name = MetricsTestScope.UniqueName(prefix);
            meter = provider.CreateMeter("Namespace", name, "D1");
            return "Namespace." + name;
        }

        // Eight threads record phase 1; then a flush starts on another thread and its export is held open while the threads
        // record phase 2. The held export must carry exactly phase 1 and the next export exactly phase 2: nothing is lost and
        // nothing is exported twice when Record overlaps an in-flight flush.
        void AssertRecordsDuringFlushAreExportedOnce(string metric, IMeter1D<long> meter, Action flush)
        {
            const int Threads = 8;
            const int PerPhase = 25000;
            ConcurrentQueue<Exception> errors = new();
            CountdownEvent phase1Done = new(Threads);
            CountdownEvent phase2Done = new(Threads);
            ManualResetEventSlim startPhase2 = new(false);
            ManualResetEventSlim exportEntered = new(false);
            ManualResetEventSlim releaseExport = new(false);
            TestMetricExporter exporter = scope.Exporter;
            Thread[] recorders = null;
            Thread flusher = null;
            try
            {
                recorders = RecorderThreads.Start(Threads, errors, () =>
                {
                    try
                    {
                        RecordOnes(meter, PerPhase);
                    }
                    finally
                    {
                        _ = phase1Done.Signal();
                    }

                    if (startPhase2.Wait(RecorderThreads.Timeout))
                    {
                        try
                        {
                            RecordOnes(meter, PerPhase);
                        }
                        finally
                        {
                            _ = phase2Done.Signal();
                        }
                    }
                });

                AssertSignaled(phase1Done.Wait(RecorderThreads.Timeout), "Phase 1 did not finish.", errors);
                Assert.Empty(errors);
                long exportedBefore = ExportedCount(exporter, metric);
                exporter.BeforeExport = () =>
                {
                    exportEntered.Set();
                    _ = releaseExport.Wait(RecorderThreads.Timeout);
                };

                flusher = new Thread(() => flush()) { IsBackground = true };
                flusher.Start();
                AssertSignaled(exportEntered.Wait(RecorderThreads.Timeout), "The flush never reached the exporter.", errors);

                startPhase2.Set();
                AssertSignaled(phase2Done.Wait(RecorderThreads.Timeout), "Phase 2 did not finish while the export was held open.", errors);

                exporter.BeforeExport = null;
                releaseExport.Set();
                Assert.True(flusher.Join(RecorderThreads.Timeout), "The flush did not finish.");
                RecorderThreads.JoinAll(recorders);

                Assert.Empty(errors);

                // The held export must carry exactly the measurements recorded before it collected.
                Assert.Equal((long)Threads * PerPhase, ExportedCount(exporter, metric) - exportedBefore);

                // Measurements recorded while the export was held open must be exported once, by the next export.
                Assert.Equal(2L * Threads * PerPhase, scope.FlushAndGet(metric).Sum(p => p.Count));
            }
            finally
            {
                // Release everything so no thread outlives a failed test blocked on a gate.
                exporter.BeforeExport = null;
                startPhase2.Set();
                releaseExport.Set();
                _ = flusher?.Join(RecorderThreads.Timeout);
                foreach (Thread recorder in recorders ?? [])
                    _ = recorder.Join(RecorderThreads.Timeout);
            }
        }

        static void RecordOnes(IMeter1D<long> meter, int count)
        {
            for (int i = 0; i < count; i++)
                meter.Record(1, "a");
        }

        static long ExportedCount(TestMetricExporter exporter, string metric) =>
            exporter.Points.Where(p => p.MetricName == metric).Sum(p => p.Count);

        static void AssertSignaled(bool signaled, string message, ConcurrentQueue<Exception> errors) =>
            Assert.True(signaled, message + " Recorder errors: " + string.Join(Environment.NewLine, errors));
    }

    void AssertFixedDimensions(ExportedPoint point)
    {
        Assert.Equal(context.PartitionId.ToString(), point.Tag(nameof(ServiceContext.PartitionId)));
        Assert.Equal(context.ServiceTypeName, point.Tag(nameof(ServiceContext.ServiceTypeName)));
        Assert.Equal(context.ServiceName.ToString(), point.Tag(nameof(ServiceContext.ServiceName)));
        Assert.Equal(context.CodePackageActivationContext.ApplicationName, point.Tag(nameof(ICodePackageActivationContext.ApplicationName)));
        Assert.Equal(context.CodePackageActivationContext.ApplicationTypeName, point.Tag(nameof(ICodePackageActivationContext.ApplicationTypeName)));
    }
}
