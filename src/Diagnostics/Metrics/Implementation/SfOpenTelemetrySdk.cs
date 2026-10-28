// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Metrics;
using OpenTelemetryMeterProvider = OpenTelemetry.Metrics.MeterProvider;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Builds Service Fabric's OpenTelemetry SDK meter provider: one periodic delta reader with a Geneva exporter per valid
/// collector, exporting only instruments of Service Fabric's own meter. OpenTelemetry SDK, exporter and
/// Microsoft.Extensions types are referenced only from this class, so they are loaded only when a provider is built.
/// </summary>
static class SfOpenTelemetrySdk
{
    const string TraceType = "OpenTelemetryMetricsFactory";

    // The native implementation's PeriodicExportingMetricReaderOptions defaults.
    const int ExportIntervalMilliseconds = 60000;
    const int ExportTimeoutMilliseconds = 30000;

    // The Geneva exporter requires OpenTelemetry 1.15.3 or later, but every OpenTelemetry 1.x assembly has version 1.0.0.0, so on
    // .NET Framework an app's older copy can load instead; only the file version tells them apart.
    static readonly Version minimumOpenTelemetryVersion = new(1, 15, 3);

    // Replaced by tests to export to an in-memory exporter.
    internal static Func<string, IReadOnlyDictionary<string, object>, BaseExporter<Metric>> CreateExporter = CreateGenevaExporter;

    // Replaced by tests to simulate other OpenTelemetry versions.
    internal static Func<Assembly, Version> ReadFileVersion = ReadAssemblyFileVersion;

    static readonly FailureTraceThrottle flushFailures = new();

    /// <summary>
    /// Throws when a loaded OpenTelemetry assembly is older than the Geneva exporter requires. Call before <see cref="Build"/>,
    /// which may not even compile against an older OpenTelemetry.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void EnsureSupportedVersion()
    {
        Assembly[] assemblies =
            [typeof(Sdk).Assembly, typeof(Baggage).Assembly, typeof(OpenTelemetryDependencyInjectionMeterProviderBuilderExtensions).Assembly];
        foreach (Assembly assembly in assemblies)
        {
            Version version = ReadFileVersion(assembly);
            if (version != null && version < minimumOpenTelemetryVersion)
            {
                throw new NotSupportedException(
                    $"{assembly.GetName().Name} {version} loaded from '{assembly.Location}' is older than OpenTelemetry {minimumOpenTelemetryVersion}, which the Geneva exporter requires.");
            }
        }
    }

    /// <summary>
    /// Returns the SDK meter provider, or null when no collector yields an exporter. Throws when the provider cannot be built.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IDisposable Build(MetricsSettings settings)
    {
        IReadOnlyDictionary<string, object> prepopulatedDimensions = GenevaCollectorRules.FilterPrepopulatedDimensions(settings.Dimensions);
        List<MetricReader> readers = [];
        try
        {
            foreach (string connectionString in settings.GenevaConnectionStrings)
            {
                if (!GenevaCollectorRules.IsValidConnectionString(connectionString, GenevaCollectorRules.IsLinux))
                {
                    MetricsTrace.Warning(
                        TraceType,
                        $"Geneva collector connection string '{connectionString}' does not yield a supported metrics transport on this platform; skipping this collector. Metrics for this collector are disabled.");
                    continue;
                }

                BaseExporter<Metric> exporter;
                try
                {
                    exporter = CreateExporter(connectionString, prepopulatedDimensions);
                }
                catch (Exception e)
                {
                    MetricsTrace.Warning(
                        TraceType,
                        $"Geneva collector connection string '{connectionString}' was rejected by the Geneva exporter; skipping this collector. Metrics for this collector are disabled. Error: {e}");
                    continue;
                }

                readers.Add(new PeriodicExportingMetricReader(exporter, ExportIntervalMilliseconds, ExportTimeoutMilliseconds)
                {
                    TemporalityPreference = MetricReaderTemporalityPreference.Delta,
                });
            }

            if (readers.Count == 0)
            {
                MetricsTrace.Info(TraceType, "No valid Geneva collector is configured; managed metrics are recorded but not exported.");
                return null;
            }

            MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder()
                // Ignore OTEL_* environment variables of the host process, such as OTEL_SDK_DISABLED.
                .ConfigureServices(static services => services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()))
                .AddMeter(SfMeterRegistry.MeterName)
                // Export only instruments of Service Fabric's own meter object, not same-named meters created by other code.
                .AddView(static instrument => ReferenceEquals(instrument.Meter, SfMeterRegistry.Meter) ? null : MetricStreamConfiguration.Drop)
                .SetExemplarFilter(ExemplarFilterType.AlwaysOff);

            foreach (MetricReader reader in readers)
                _ = builder.AddReader(reader);

            OpenTelemetryMeterProvider provider = builder.Build();
            MetricsTrace.Info(TraceType, $"Managed metrics export to {readers.Count} Geneva collector(s).");
            return provider;
        }
        catch (Exception)
        {
            foreach (MetricReader reader in readers)
                reader.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Exports everything recorded so far. Waits for all readers without a timeout, like the native ForceFlush. A flush that
    /// does not complete for every collector is traced, throttled; each collector's exporter traces its own failed exports.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ForceFlush(IDisposable provider)
    {
        bool completed = ((OpenTelemetryMeterProvider)provider).ForceFlush(Timeout.Infinite);
        long incompleteFlushes;
        if (completed)
        {
            if (flushFailures.OnSuccess(out incompleteFlushes))
                MetricsTrace.Info(TraceType, $"Managed metrics flush completed for every Geneva collector again after {incompleteFlushes} incomplete flush(es).");
        }
        else if (flushFailures.OnFailure(out incompleteFlushes))
        {
            MetricsTrace.Warning(
                TraceType,
                $"Managed metrics flush did not complete for every Geneva collector ({incompleteFlushes} incomplete flush(es) since the last such trace); failed exports are traced per collector.");
        }
    }

    static BaseExporter<Metric> CreateGenevaExporter(string connectionString, IReadOnlyDictionary<string, object> prepopulatedDimensions)
    {
        GenevaMetricExporterOptions options = new() { ConnectionString = connectionString };
        if (prepopulatedDimensions.Count > 0)
            options.PrepopulatedMetricDimensions = prepopulatedDimensions;

        return new TracingGenevaMetricExporter(options, connectionString);
    }

    static Version ReadAssemblyFileVersion(Assembly assembly)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(assembly.Location);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }
        catch (Exception)
        {
            // An assembly without a file, such as one loaded from bytes, is not checked.
            return null;
        }
    }

    /// <summary>
    /// The Geneva exporter reports a failed export only through its result and its own EventSource
    /// (OpenTelemetry-Exporter-Geneva), so this subclass also traces it to the Service Fabric trace.
    /// It subclasses rather than wraps the exporter, so the reader still sets the exporter's ParentProvider.
    /// </summary>
    sealed class TracingGenevaMetricExporter(GenevaMetricExporterOptions options, string collector) : GenevaMetricExporter(options)
    {
        readonly string collector = collector;
        readonly FailureTraceThrottle failures = new();

        public override ExportResult Export(in Batch<Metric> batch)
        {
            ExportResult result;
            try
            {
                result = base.Export(batch);
            }
            catch (Exception e)
            {
                TraceFailure(e);
                throw;
            }

            if (result != ExportResult.Success)
                TraceFailure(null);
            else if (failures.OnSuccess(out long failedExports))
                MetricsTrace.Info(TraceType, $"Managed metrics export to Geneva collector '{collector}' succeeded again after {failedExports} failed export(s).");

            return result;
        }

        void TraceFailure(Exception e)
        {
            if (failures.OnFailure(out long failedExports))
            {
                string error = e == null ? string.Empty : $" Error: {e}";
                MetricsTrace.Warning(
                    TraceType,
                    $"Managed metrics export to Geneva collector '{collector}' failed ({failedExports} failed export(s) since the last such trace); points that were not sent are not retried. The exporter writes the reason for each point to the OpenTelemetry-Exporter-Geneva EventSource.{error}");
            }
        }
    }

    /// <summary>
    /// Throttles traces of a repeating failure: the first failure is traced, then at most one trace per interval carries the
    /// count since the previous trace, and the first success after failures is traced once. Exports run once a minute per
    /// collector, so a count-based throttle (MetricsTrace.ShouldTraceOccurrence) would trace too rarely.
    /// </summary>
    sealed class FailureTraceThrottle
    {
        static readonly long intervalTimestampTicks = Stopwatch.Frequency * 300;

        readonly object syncRoot = new();
        long failuresSinceTrace;
        long failuresSinceSuccess;
        long nextTraceTimestamp;
        bool failing;

        /// <summary>
        /// Counts a failure. Returns true when it is to be traced, with the number of failures since the previous trace.
        /// </summary>
        internal bool OnFailure(out long failuresSinceLastTrace)
        {
            lock (syncRoot)
            {
                Volatile.Write(ref failing, true);
                failuresSinceSuccess++;
                failuresSinceTrace++;

                long now = Stopwatch.GetTimestamp();
                if (now < nextTraceTimestamp)
                {
                    failuresSinceLastTrace = 0;
                    return false;
                }

                failuresSinceLastTrace = failuresSinceTrace;
                failuresSinceTrace = 0;
                nextTraceTimestamp = now + intervalTimestampTicks;
                return true;
            }
        }

        /// <summary>
        /// Records a success. Returns true for the first success after failures, with the number of those failures.
        /// </summary>
        internal bool OnSuccess(out long failuresBeforeSuccess)
        {
            failuresBeforeSuccess = 0;
            if (!Volatile.Read(ref failing))
                return false;

            lock (syncRoot)
            {
                if (!failing)
                    return false;

                failuresBeforeSuccess = failuresSinceSuccess;
                failuresSinceSuccess = 0;
                failuresSinceTrace = 0;
                nextTraceTimestamp = 0;
                Volatile.Write(ref failing, false);
                return true;
            }
        }
    }
}
