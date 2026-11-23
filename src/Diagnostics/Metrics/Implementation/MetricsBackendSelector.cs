// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Chooses the metrics backend of the process (AppDomain or load context) once, when the first meter provider is created, from
/// the Telemetry/Metrics ManagedMetricsBackend cluster setting and the framework the app was built for. OpenTelemetry is the
/// default, so a cluster sets ComInterop to opt out. The native Service Fabric runtime declares the setting with the same values
/// and the same default.
/// </summary>
static class MetricsBackendSelector
{
    internal const string SettingKey = "ManagedMetricsBackend";

    const string Setting = MetricsSettings.MetricsSection + "/" + SettingKey;
    const string TraceType = SettingKey;

    // The OpenTelemetry backend supports apps built for .NET 10 or later, or for .NET Framework 4.6.2 or later, the oldest
    // .NET Framework that OpenTelemetry supports.
    static readonly Version minimumDotNetVersion = new(10, 0);
    static readonly Version minimumDotNetFrameworkVersion = new(4, 6, 2);

    // Replaced by tests.
    internal static Func<IMetricsSettingsSource> CreateSettingsSource = static () => new RuntimeMetricsSettingsSource();

    // Replaced by tests.
    internal static Func<string> ReadAppTargetFramework = ReadEntryAssemblyTargetFramework;

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
    /// Forgets the chosen backend, so the next meter provider chooses it again.
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

        string setting;
        if (string.IsNullOrEmpty(value))
        {
            setting = $"{Setting} is not set, so the default {MetricsBackend.OpenTelemetry} applies";
        }
        else if (Is(value, MetricsBackend.OpenTelemetry))
        {
            setting = $"{Setting} is '{value}'";
        }
        else if (Is(value, MetricsBackend.ComInterop))
        {
            return Choose(MetricsBackend.ComInterop, $"{Setting} is '{value}'.");
        }
        else
        {
            // A cluster sets a value only to opt out of the default, so an unknown one uses COM interop too.
            MetricsTrace.Warning(TraceType, $"{Setting} has an unknown value '{value}'; using {MetricsBackend.ComInterop}.");
            return Choose(MetricsBackend.ComInterop, $"{Setting} has an unknown value '{value}'.");
        }

        // Checked before anything loads OpenTelemetry, so apps built for an older framework never load it.
        string appFramework = ReadAppTargetFramework();
        if (!IsSupportedAppFramework(appFramework))
        {
            return Choose(
                MetricsBackend.ComInterop,
                $"{setting}, but the app is built for {(string.IsNullOrEmpty(appFramework) ? "an unknown framework" : appFramework)}, " +
                $"and the OpenTelemetry backend needs .NET {minimumDotNetVersion} or later, or .NET Framework {minimumDotNetFrameworkVersion} or later.");
        }

        try
        {
            SfMetricsPipeline.Start();
        }
        catch (Exception e)
        {
            MetricsTrace.Warning(TraceType, $"{setting}, but OpenTelemetry could not start; using {MetricsBackend.ComInterop}. Error: {e}");
            return Choose(MetricsBackend.ComInterop, "OpenTelemetry could not start.");
        }

        return Choose(MetricsBackend.OpenTelemetry, $"{setting}.");
    }

    /// <summary>
    /// Returns the framework the app was built for, such as ".NETCoreApp,Version=v10.0", from the TargetFrameworkAttribute of its
    /// entry assembly, or null when the process has no entry assembly, such as under a native host, or the attribute is missing.
    /// </summary>
    internal static string ReadEntryAssemblyTargetFramework() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

    /// <summary>
    /// Whether the OpenTelemetry backend supports an app built for the framework. It is the framework the app was built for, not the
    /// runtime it runs on, so an app built for .NET 8 that rolls forward to .NET 10 is not supported.
    /// </summary>
    static bool IsSupportedAppFramework(string appFramework)
    {
        if (string.IsNullOrEmpty(appFramework))
            return false;

        FrameworkName framework;
        try
        {
            framework = new FrameworkName(appFramework);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return framework.Identifier switch
        {
            ".NETCoreApp" => framework.Version >= minimumDotNetVersion,
            ".NETFramework" => framework.Version >= minimumDotNetFrameworkVersion,
            _ => false,
        };
    }

    static bool Is(string value, MetricsBackend backend) =>
        string.Equals(value, backend.ToString(), StringComparison.OrdinalIgnoreCase);

    static MetricsBackend Choose(MetricsBackend backend, string reason)
    {
        MetricsTrace.Info(TraceType, $"Meters of this process use the {backend} metrics backend. {reason}");
        return backend;
    }
}
