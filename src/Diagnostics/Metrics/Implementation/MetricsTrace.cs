// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric.Common;
using System.Threading;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Writes metrics diagnostics to the Service Fabric trace, the sink the native implementation also writes to. Trace types match
/// the native component names where there is one (FabricMeter, MetricsSettings, OpenTelemetryMetricsFactory,
/// FabricTelemetry.Meter).
/// </summary>
static class MetricsTrace
{
    /// <summary>
    /// Repeated hot-path problems are traced on the first occurrence and then every 1000th, like native
    /// MeterBase::RecordToInstrument (RecordFailureTraceInterval).
    /// </summary>
    internal const long RepeatedEventTraceInterval = 1000;

    // Replaced by tests to observe traces without the Service Fabric runtime.
    internal static Action<MetricsTraceLevel, string, string> Sink = WriteToServiceFabricTrace;

    internal static void Info(string type, string message) => Write(MetricsTraceLevel.Info, type, message);

    internal static void Warning(string type, string message) => Write(MetricsTraceLevel.Warning, type, message);

    internal static void Error(string type, string message) => Write(MetricsTraceLevel.Error, type, message);

    /// <summary>
    /// Counts one occurrence and returns whether to trace it: the first one and then every <see cref="RepeatedEventTraceInterval"/>th.
    /// </summary>
    internal static bool ShouldTraceOccurrence(ref long counter, out long occurrence)
    {
        occurrence = Interlocked.Increment(ref counter);
        return occurrence == 1 || occurrence % RepeatedEventTraceInterval == 0;
    }

    static void Write(MetricsTraceLevel level, string type, string message)
    {
        try
        {
            Sink(level, type, message);
        }
        catch (Exception)
        {
            // A tracing failure must never fail the metrics call that reported it.
        }
    }

    static void WriteToServiceFabricTrace(MetricsTraceLevel level, string type, string message)
    {
        switch (level)
        {
            case MetricsTraceLevel.Error:
                AppTrace.TraceSource.WriteError(type, "{0}", message);
                break;
            case MetricsTraceLevel.Warning:
                AppTrace.TraceSource.WriteWarning(type, "{0}", message);
                break;
            default:
                AppTrace.TraceSource.WriteInfo(type, "{0}", message);
                break;
        }
    }
}
