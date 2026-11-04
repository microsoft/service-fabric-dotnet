// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using DiagnosticsMeter = System.Diagnostics.Metrics.Meter;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Owns Service Fabric's <see cref="DiagnosticsMeter"/> and its instruments for the process (AppDomain or load context).
/// Every metric name maps to exactly one <see cref="Histogram{T}"/>, shared by all meter providers and meters that use the
/// name. Instruments live for the life of the process.
/// </summary>
static class SfMeterRegistry
{
    /// <summary>
    /// Meter (instrumentation scope) name and version, the same as the native implementation's.
    /// </summary>
    internal const string MeterName = "Microsoft.ServiceFabric";
    internal const string MeterVersion = "1.0";

    internal static readonly DiagnosticsMeter Meter = new(MeterName, MeterVersion);

    // Lazy publishes each histogram once even when meters for a new name are created concurrently.
    static readonly ConcurrentDictionary<string, Lazy<Histogram<long>>> histograms = new(StringComparer.Ordinal);

    static readonly Func<string, Lazy<Histogram<long>>> createHistogram =
        static name => new Lazy<Histogram<long>>(() => Meter.CreateHistogram<long>(name), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Loads System.Diagnostics.DiagnosticSource and initializes the meter. Throws when the assembly cannot be loaded.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void EnsureLoaded() => GC.KeepAlive(Meter);

    internal static Histogram<long> GetOrCreateHistogram(string name) => histograms.GetOrAdd(name, createHistogram).Value;
}
