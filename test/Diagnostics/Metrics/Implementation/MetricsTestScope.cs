// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Inspector;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Makes the next meter provider choose the OpenTelemetry backend with fake settings, test exporters and an in-memory trace
/// sink, and restores the process-wide state on dispose. Tests using it must belong to <see cref="MetricsPipelineCollection"/>.
/// </summary>
sealed class MetricsTestScope : IDisposable
{
    /// <summary>
    /// A Geneva connection string the native validation accepts on this platform.
    /// </summary>
    internal static readonly string ValidConnectionString = GenevaCollectorRules.IsLinux
        ? "Endpoint=unix:///var/run/sf-metrics-test.socket;Account=TestAccount;Namespace=TestNamespace"
        : "Account=TestAccount;Namespace=TestNamespace";

    static int nameCounter;

    internal readonly FakeMetricsSettingsSource Source = new();

    readonly Lazy<MetricsBackend> backend = typeof(MetricsBackendSelector).Field<Lazy<MetricsBackend>>().Value;
    readonly Func<IMetricsSettingsSource> createSettingsSource = MetricsBackendSelector.CreateSettingsSource;
    readonly Func<MetricsSettings> loadSettings = SfMetricsPipeline.LoadSettings;
    readonly Func<MetricsSettings, IDisposable> buildSdkProvider = SfMetricsPipeline.BuildSdkProvider;
    readonly Func<string, IReadOnlyDictionary<string, object>, BaseExporter<Metric>> createExporter = SfOpenTelemetrySdk.CreateExporter;
    readonly Func<Assembly, Version> readFileVersion = SfOpenTelemetrySdk.ReadFileVersion;
    readonly Action loadGenevaExporter = SfOpenTelemetrySdk.LoadGenevaExporter;
#if !NET
    readonly Func<string> getAssemblyFolder = OpenTelemetryAssemblyResolver.GetFolder;
#endif
    readonly Action<MetricsTraceLevel, string, string> sink = MetricsTrace.Sink;
    readonly List<TestMetricExporter> exporters = [];
    readonly List<TraceRecord> traces = [];

    internal MetricsTestScope(bool withCollector = true)
    {
        MetricsTrace.Sink = (level, type, message) =>
        {
            lock (traces)
                traces.Add(new TraceRecord(level, type, message));
        };

        SfMetricsPipeline.ResetForTesting();
#if !NET
        // Keeps tests from loading assemblies of an installed Service Fabric runtime.
        OpenTelemetryAssemblyResolver.GetFolder = static () => string.Empty;
#endif

        _ = Source.Set(MetricsSettings.MetricsSection, MetricsBackendSelector.SettingKey, nameof(MetricsBackend.OpenTelemetry));
        if (withCollector)
            _ = Source.SetCollectors(ValidConnectionString);

        MetricsBackendSelector.CreateSettingsSource = () => Source;
        MetricsBackendSelector.ResetForTesting();
        SfMetricsPipeline.LoadSettings = () => MetricsSettings.Load(Source);
        SfOpenTelemetrySdk.CreateExporter = (connectionString, dimensions) =>
        {
            TestMetricExporter exporter = new(connectionString, dimensions);
            lock (exporters)
                exporters.Add(exporter);

            return exporter;
        };
    }

    internal IReadOnlyList<TestMetricExporter> Exporters
    {
        get
        {
            lock (exporters)
                return exporters.ToArray();
        }
    }

    internal TestMetricExporter Exporter => Exporters.Single();

    internal IReadOnlyList<TraceRecord> Traces
    {
        get
        {
            lock (traces)
                return traces.ToArray();
        }
    }

    /// <summary>
    /// Returns a name used by no other test, so points recorded by earlier tests never mix in.
    /// </summary>
    internal static string UniqueName(string prefix) => prefix + Interlocked.Increment(ref nameCounter);

    /// <summary>
    /// Exports everything recorded so far and returns the exported points of one metric ("namespace.name").
    /// </summary>
    internal IReadOnlyList<ExportedPoint> FlushAndGet(string metricName)
    {
        SfMetricsPipeline.ForceFlush();
        return Exporter.Points.Where(p => p.MetricName == metricName).ToArray();
    }

    internal IReadOnlyList<TraceRecord> TracesOfType(string type) => Traces.Where(t => t.Type == type).ToArray();

    public void Dispose()
    {
        SfMetricsPipeline.ResetForTesting();
        SfMetricsPipeline.LoadSettings = loadSettings;
        SfMetricsPipeline.BuildSdkProvider = buildSdkProvider;
        SfOpenTelemetrySdk.CreateExporter = createExporter;
        SfOpenTelemetrySdk.ReadFileVersion = readFileVersion;
        SfOpenTelemetrySdk.LoadGenevaExporter = loadGenevaExporter;
#if !NET
        OpenTelemetryAssemblyResolver.ResetForTesting();
        OpenTelemetryAssemblyResolver.GetFolder = getAssemblyFolder;
#endif
        MetricsBackendSelector.CreateSettingsSource = createSettingsSource;
        typeof(MetricsBackendSelector).Field<Lazy<MetricsBackend>>().Set(backend);
        MetricsTrace.Sink = sink;
    }
}
