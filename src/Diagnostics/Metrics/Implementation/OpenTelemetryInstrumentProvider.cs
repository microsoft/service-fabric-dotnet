// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Creates instruments that record in process, applying the rules the native implementation applies on the COM interop path:
/// meter name validation, the Telemetry/Metrics configuration gate, and an export of the recorded values when the provider is
/// released.
/// </summary>
sealed class OpenTelemetryInstrumentProvider : IInstrumentProvider
{
    // HRESULT with which the native implementation rejects a meter it cannot create; COM interop surfaces it as COMException.
    const int E_FAIL = unchecked((int)0x80004005);

    readonly bool metricsEnabled = SfMetricsPipeline.MetricsEnabled;
    readonly KeyValuePair<string, object>[] fixedTags;
    int disposed;

    internal OpenTelemetryInstrumentProvider(IReadOnlyCollection<string> fixedDimensionNames, IReadOnlyCollection<string> fixedDimensionValues) =>
        fixedTags = [.. fixedDimensionNames.Zip(fixedDimensionValues, FixedTag)];

    IInstrument IInstrumentProvider.CreateInstrument(string metricNamespace, string name, string[] variableDimensionNames)
    {
        if (Volatile.Read(ref disposed) != 0)
            throw new ObjectDisposedException(nameof(MeterProvider<>));

        // The native implementation returns E_INVALIDARG, which COM interop surfaces as ArgumentException.
        if (string.IsNullOrEmpty(metricNamespace))
            throw new ArgumentException("The metric namespace cannot be null or empty.", nameof(metricNamespace));
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("The metric name cannot be null or empty.", nameof(name));

        if (!metricsEnabled)
            return new OpenTelemetryInstrument(null);

        if (metricNamespace == ".")
            throw new COMException("The metric namespace cannot be a period character '.'.", E_FAIL);
        if (name == ".")
            throw new COMException("The metric name cannot be a period character '.'.", E_FAIL);

        for (int i = 0; i < variableDimensionNames.Length; i++)
            if (string.IsNullOrEmpty(variableDimensionNames[i]))
                throw new COMException($"Dimension name {i} cannot be empty.", E_FAIL);

        return new OpenTelemetryInstrument(SfMetricsPipeline.CreateInstrument(metricNamespace + "." + name, fixedTags, variableDimensionNames));
    }

    /// <summary>
    /// Exports everything recorded so far, like the native provider's destructor. Instruments of this provider keep working.
    /// </summary>
    void IDisposable.Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && metricsEnabled)
            SfMetricsPipeline.ForceFlush();
    }

    // Perf optimization: the values carry their hash, so the SDK does not hash these strings on every record (see SfTagValue).
    static KeyValuePair<string, object> FixedTag(string key, string value) => new(key, SfTagValue.For(key, value));
}
