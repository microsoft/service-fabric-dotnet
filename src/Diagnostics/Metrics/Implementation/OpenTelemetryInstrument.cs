// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Records the values of one meter in process. Like native FabricMeter::Record, it clamps a negative value to 0 for histogram
/// recording, whether or not metrics are enabled.
/// </summary>
sealed class OpenTelemetryInstrument(SfInstrument instrument) : IInstrument
{
    const string NegativeValueTraceType = "FabricMeter";

    // Null when metrics are disabled: the meter keeps its API behavior and records nothing.
    readonly SfInstrument instrument = instrument;
    volatile bool disposed;
    long negativeValueCount;

    void IInstrument.Record(long value)
    {
        ThrowIfDisposed();
        long recordableValue = ToRecordableValue(value);
        instrument?.Record(recordableValue);
    }

    void IInstrument.Record(long value, string dimension1Value)
    {
        ThrowIfDisposed();
        long recordableValue = ToRecordableValue(value);
        instrument?.Record(recordableValue, dimension1Value);
    }

    void IInstrument.Record(long value, string dimension1Value, string dimension2Value)
    {
        ThrowIfDisposed();
        long recordableValue = ToRecordableValue(value);
        instrument?.Record(recordableValue, dimension1Value, dimension2Value);
    }

    void IInstrument.Record(long value, string dimension1Value, string dimension2Value, string dimension3Value)
    {
        ThrowIfDisposed();
        long recordableValue = ToRecordableValue(value);
        instrument?.Record(recordableValue, dimension1Value, dimension2Value, dimension3Value);
    }

    void IDisposable.Dispose() => disposed = true;

    void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(Meter));
    }

    long ToRecordableValue(long value)
    {
        if (value >= 0)
            return value;

        if (MetricsTrace.ShouldTraceOccurrence(ref negativeValueCount, out long count))
            MetricsTrace.Warning(NegativeValueTraceType, $"Negative value {value} clamped to 0 for histogram recording (occurrence {count}).");

        return 0;
    }
}
