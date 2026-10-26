// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Rules for configuring a Geneva collector: the native implementation's connection string validation and the
/// limits OpenTelemetry.Exporter.Geneva enforces on prepopulated dimensions.
/// </summary>
static class GenevaCollectorRules
{
    const string TraceType = "OpenTelemetryMetricsFactory";

    // GenevaMetricExporter.MaxDimensionNameSize, MaxDimensionValueSize and reserved dimension keys (OpenTelemetry.Exporter.Geneva 1.15.2).
    internal const int MaxDimensionNameLength = 256;
    internal const int MaxDimensionValueLength = 1024;
    internal const string ReservedAccountDimension = "_microsoft_metrics_account";
    internal const string ReservedNamespaceDimension = "_microsoft_metrics_namespace";

    internal static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Port of native IsValidGenevaConnectionString (OpenTelemetryMetricsFactory.cpp). Keys are case-sensitive.
    /// Windows needs Account and Namespace and no Endpoint (ETW). Linux needs an Endpoint with the "unix" scheme.
    /// </summary>
    internal static bool IsValidConnectionString(string connectionString, bool isLinux)
    {
        if (connectionString == null)
            return false;

        string account = null;
        string metricNamespace = null;
        string endpointScheme = null;
        bool endpointPresent = false;

        int position = 0;
        while (position < connectionString.Length)
        {
            int segmentEnd = connectionString.IndexOf(';', position);
            string segment = segmentEnd < 0
                ? connectionString.Substring(position)
                : connectionString.Substring(position, segmentEnd - position);
            position = segmentEnd < 0 ? connectionString.Length : segmentEnd + 1;

            int equalsPosition = segment.IndexOf('=');
            if (equalsPosition < 0)
                continue;

            string key = segment.Substring(0, equalsPosition);
            string value = segment.Substring(equalsPosition + 1);

            if (key == "Account")
            {
                account = value;
            }
            else if (key == "Namespace")
            {
                metricNamespace = value;
            }
            else if (key == "Endpoint")
            {
                endpointPresent = true;
                int schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
                if (schemeEnd >= 0)
                    endpointScheme = value.Substring(0, schemeEnd);
            }
        }

        if (isLinux)
            return endpointPresent && endpointScheme == "unix";

        return !string.IsNullOrEmpty(account) && !string.IsNullOrEmpty(metricNamespace) && !endpointPresent;
    }

    /// <summary>
    /// Returns the dimensions the Geneva exporter accepts as prepopulated dimensions. The exporter rejects the whole
    /// collection when one pair breaks its rules, so such a pair is dropped and traced instead. A null value becomes "".
    /// Lengths are UTF-16 code units, as the exporter counts them.
    /// </summary>
    internal static IReadOnlyDictionary<string, object> FilterPrepopulatedDimensions(IEnumerable<KeyValuePair<string, string>> dimensions)
    {
        Dictionary<string, object> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> dimension in dimensions)
        {
            string name = dimension.Key;
            string value = dimension.Value ?? string.Empty;

            if (string.Equals(name, ReservedAccountDimension, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ReservedNamespaceDimension, StringComparison.OrdinalIgnoreCase))
            {
                MetricsTrace.Warning(TraceType, $"Dimension '{name}' is reserved by the Geneva exporter; it is not added to exported metrics.");
                continue;
            }

            if (name.Length > MaxDimensionNameLength)
            {
                MetricsTrace.Warning(
                    TraceType,
                    $"Dimension name '{name}' is longer than {MaxDimensionNameLength} characters; it is not added to exported metrics.");
                continue;
            }

            if (value.Length > MaxDimensionValueLength)
            {
                MetricsTrace.Warning(
                    TraceType,
                    $"Value of dimension '{name}' is longer than {MaxDimensionValueLength} characters; it is not added to exported metrics.");
                continue;
            }

            result[name] = value;
        }

        return result;
    }
}
