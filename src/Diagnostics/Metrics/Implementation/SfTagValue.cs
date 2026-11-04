// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// A tag value that carries the hash code of its string. On every record the OpenTelemetry SDK hashes each tag key and value
/// to find the time series (Tags.ComputeHashCode), which for a string value means hashing the whole string each time; this
/// type is hashed once, when it is created. Exporters write tag values with Convert.ToString, which returns
/// <see cref="ToString"/>, so the exported dimensions are the same strings.
/// </summary>
/// <remarks>
/// Equal values are equal whatever the instance, so they fall into the same time series. The SDK never treats an
/// <see cref="SfTagValue"/> and a string as equal, so every record of a dimension value must use the same representation:
/// <see cref="SfHistogramInstrument"/> wraps dimension values on every path.
/// </remarks>
sealed class SfTagValue(string value)
{
    readonly string value = value;

    // Perf optimization: hash the string once here instead of in the SDK on every record.
    readonly int hashCode = StringComparer.Ordinal.GetHashCode(value);

    /// <summary>
    /// Returns the value to record for a tag: an <see cref="SfTagValue"/>, or the string itself when it is null or the key is
    /// a Geneva routing key, which the Geneva exporter reads only from string values.
    /// </summary>
    internal static object For(string key, string value) =>
        IsWrapped(key, value) ? new SfTagValue(value) : value;

    internal static bool IsWrapped(string key, string value) =>
        value != null &&
        !string.Equals(key, GenevaCollectorRules.ReservedAccountDimension, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(key, GenevaCollectorRules.ReservedNamespaceDimension, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object obj) =>
        obj is SfTagValue other &&
        (ReferenceEquals(this, other) || (hashCode == other.hashCode && string.Equals(value, other.value, StringComparison.Ordinal)));

    public override int GetHashCode() => hashCode;

    public override string ToString() => value;
}
