// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
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

    // Replaced by tests to export to an in-memory exporter.
    internal static Func<string, IReadOnlyDictionary<string, object>, BaseExporter<Metric>> CreateExporter = CreateGenevaExporter;

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
    /// Exports everything recorded so far. Waits for all readers without a timeout, like the native ForceFlush.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ForceFlush(IDisposable provider) =>
        _ = ((OpenTelemetryMeterProvider)provider).ForceFlush(Timeout.Infinite);

    static BaseExporter<Metric> CreateGenevaExporter(string connectionString, IReadOnlyDictionary<string, object> prepopulatedDimensions)
    {
        GenevaMetricExporterOptions options = new() { ConnectionString = connectionString };
        if (prepopulatedDimensions.Count > 0)
            options.PrepopulatedMetricDimensions = prepopulatedDimensions;

        return new GenevaMetricExporter(options);
    }
}
