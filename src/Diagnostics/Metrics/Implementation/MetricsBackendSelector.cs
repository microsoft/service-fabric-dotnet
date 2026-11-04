// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Chooses the metrics backend of the process (AppDomain or load context) once, when the first meter provider is created, from
/// the Telemetry/Metrics ManagedMetricsBackend cluster setting. The native Service Fabric runtime declares the setting with the
/// same values and the same default.
/// </summary>
static class MetricsBackendSelector
{
    internal const string SettingKey = "ManagedMetricsBackend";

    const string Setting = MetricsSettings.MetricsSection + "/" + SettingKey;
    const string TraceType = SettingKey;

    // Replaced by tests.
    internal static Func<IMetricsSettingsSource> CreateSettingsSource = static () => new RuntimeMetricsSettingsSource();

    static Lazy<MetricsBackend> selected = new(Select);

    /// <summary>
    /// Gets the metrics backend of this process, choosing it when no meter provider has done so yet.
    /// </summary>
    internal static MetricsBackend Backend => selected.Value;

    internal static IInstrumentProvider CreateInstrumentProvider(
        IReadOnlyCollection<string> fixedDimensionNames, IReadOnlyCollection<string> fixedDimensionValues) =>
        Backend == MetricsBackend.OpenTelemetry
            ? new OpenTelemetryInstrumentProvider(fixedDimensionNames, fixedDimensionValues)
            : new ComInstrumentProvider(fixedDimensionNames, fixedDimensionValues);

    /// <summary>
    /// Test hook: forgets the chosen backend, so the next meter provider chooses it again.
    /// </summary>
    internal static void ResetForTesting() => selected = new Lazy<MetricsBackend>(Select);

    static MetricsBackend Select()
    {
        string value;
        try
        {
            value = CreateSettingsSource().ReadConfigValue(MetricsSettings.MetricsSection, SettingKey);
        }
        catch (Exception e)
        {
            // The configuration store exists only on Service Fabric nodes, so this is also the backend of unit tests.
            return Choose(MetricsBackend.ComInterop, $"{Setting} could not be read: {e.Message}");
        }

        if (string.IsNullOrEmpty(value))
            return Choose(MetricsBackend.ComInterop, $"{Setting} is not set.");

        if (Is(value, MetricsBackend.ComInterop))
            return Choose(MetricsBackend.ComInterop, $"{Setting} is '{value}'.");

        if (!Is(value, MetricsBackend.OpenTelemetry))
        {
            MetricsTrace.Warning(TraceType, $"{Setting} has an unknown value '{value}'; using {MetricsBackend.ComInterop}.");
            return Choose(MetricsBackend.ComInterop, $"{Setting} has an unknown value '{value}'.");
        }

        try
        {
            SfMetricsPipeline.Start();
        }
        catch (Exception e)
        {
            MetricsTrace.Warning(TraceType, $"{Setting} is '{value}', but OpenTelemetry could not start; using {MetricsBackend.ComInterop}. Error: {e}");
            return Choose(MetricsBackend.ComInterop, "OpenTelemetry could not start.");
        }

        return Choose(MetricsBackend.OpenTelemetry, $"{Setting} is '{value}'.");
    }

    static bool Is(string value, MetricsBackend backend) =>
        string.Equals(value, backend.ToString(), StringComparison.OrdinalIgnoreCase);

    static MetricsBackend Choose(MetricsBackend backend, string reason)
    {
        MetricsTrace.Info(TraceType, $"Meters of this process use the {backend} metrics backend. {reason}");
        return backend;
    }
}
