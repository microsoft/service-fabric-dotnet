// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Process-wide (AppDomain or load context) state of the OpenTelemetry metrics backend: the configuration gate and Service
/// Fabric's OpenTelemetry SDK provider. <see cref="MetricsBackendSelector"/> starts it when it chooses the backend; only
/// <see cref="Start"/> throws.
/// </summary>
static class SfMetricsPipeline
{
    const string TraceType = "OpenTelemetryMetricsFactory";

    static readonly object syncRoot = new();
    static State state;

    // Replaced by tests.
    internal static Func<MetricsSettings> LoadSettings = MetricsSettings.Load;
    internal static Func<MetricsSettings, IDisposable> BuildSdkProvider = static settings => SfOpenTelemetrySdk.Build(settings);
    internal static Action<IDisposable> FlushSdkProvider = static provider => SfOpenTelemetrySdk.ForceFlush(provider);

    /// <summary>
    /// Gets a value that indicates whether the configuration enables managed metrics (the gate).
    /// </summary>
    internal static bool MetricsEnabled => Volatile.Read(ref state)?.MetricsEnabled == true;

    /// <summary>
    /// Reads the settings and, when they enable managed metrics, loads System.Diagnostics.DiagnosticSource and builds the SDK
    /// provider. Throws when the backend cannot start.
    /// </summary>
    internal static void Start()
    {
        lock (syncRoot)
            Volatile.Write(ref state, Initialize());
    }

    /// <summary>
    /// Returns the instrument for a meter, or null when it cannot be created. Call only when <see cref="MetricsEnabled"/>.
    /// </summary>
    internal static SfInstrument CreateInstrument(string name, KeyValuePair<string, object>[] fixedTags, string[] variableDimensionNames)
    {
        try
        {
            return SfHistogramInstrument.Create(name, fixedTags, variableDimensionNames);
        }
        catch (Exception e)
        {
            MetricsTrace.Error(TraceType, $"Failed to create the instrument of metric '{name}'; the meter is disabled. Error: {e}");
            return null;
        }
    }

    /// <summary>
    /// Exports everything recorded so far to every collector, without a timeout.
    /// </summary>
    internal static void ForceFlush()
    {
        IDisposable sdkProvider = Volatile.Read(ref state)?.SdkProvider;
        if (sdkProvider == null)
            return;

        try
        {
            FlushSdkProvider(sdkProvider);
        }
        catch (Exception e)
        {
            MetricsTrace.Warning(TraceType, $"Failed to flush managed metrics. Error: {e}");
        }
    }

    /// <summary>
    /// Test hook: disposes the SDK provider and forgets the state.
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (syncRoot)
        {
            State current = state;
            Volatile.Write(ref state, null);
            current?.SdkProvider?.Dispose();
        }
    }

    static State Initialize()
    {
        MetricsSettings settings = LoadSettings();
        if (!settings.IsMetricsEnabled)
        {
            MetricsTrace.Info(
                TraceType,
                $"Managed metrics are disabled by configuration (IsEnabled={settings.IsEnabled}, IsManagedTelemetryEnabled={settings.IsManagedTelemetryEnabled}).");
            return new State(metricsEnabled: false, sdkProvider: null);
        }

        // Throws when System.Diagnostics.DiagnosticSource cannot be loaded.
        SfMeterRegistry.EnsureLoaded();

        IDisposable sdkProvider = settings.GenevaConnectionStrings.Count > 0 ? BuildSdkProvider(settings) : null;
        if (sdkProvider == null && settings.GenevaConnectionStrings.Count == 0)
            MetricsTrace.Info(TraceType, "No Geneva collector is configured; managed metrics are recorded but not exported.");

        return new State(metricsEnabled: true, sdkProvider);
    }

    sealed class State(bool metricsEnabled, IDisposable sdkProvider)
    {
        internal readonly bool MetricsEnabled = metricsEnabled;
        internal readonly IDisposable SdkProvider = sdkProvider;
    }
}
