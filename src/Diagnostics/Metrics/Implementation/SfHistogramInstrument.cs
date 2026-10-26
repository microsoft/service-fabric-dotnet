// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Records into the process-wide histogram of one metric name. Each record carries the provider's fixed dimensions followed by
/// the meter's variable dimensions as tags, at most 5 + 3. The tag array for each combination of dimension values is built once
/// and recorded as a span, so a record neither builds a <see cref="TagList"/> nor copies the tags; combinations beyond the
/// cache use a per-record <see cref="TagList"/>, which stays inline (no allocation). Dimension values are recorded as
/// <see cref="SfTagValue"/> on both paths, like the provider's fixed values, so the SDK does not hash the strings per record.
/// </summary>
sealed class SfHistogramInstrument : SfInstrument
{
    const string RecordFailureTraceType = "FabricTelemetry.Meter";

    // Dimension value combinations whose tag arrays one instrument keeps. Callers pass bounded values (lock modes,
    // isolation levels, booleans, type names), so this covers them; other combinations fall back to a TagList.
    internal const int MaxCachedTagSets = 8;

    // Dimension values the per-record path keeps wrapped; past this, it wraps a value anew on each record.
    internal const int MaxFallbackTagValues = 256;

    readonly Histogram<long> histogram;
    readonly KeyValuePair<string, object>[] fixedTags;
    readonly string dimension1Name;
    readonly string dimension2Name;
    readonly string dimension3Name;
    readonly object tagSetsLock = new();

    // Copy-on-write; entries and their tag arrays are never modified once published.
    TagSet[] tagSets = [];

    // Wrapped dimension values of the per-record path; created on its first use.
    ConcurrentDictionary<string, SfTagValue> fallbackTagValues;
    int fallbackTagValuesFull;
    long recordFailureCount;

    SfHistogramInstrument(Histogram<long> histogram, KeyValuePair<string, object>[] fixedTags, string[] variableDimensionNames)
    {
        this.histogram = histogram;
        this.fixedTags = fixedTags;
        dimension1Name = variableDimensionNames.Length > 0 ? variableDimensionNames[0] : null;
        dimension2Name = variableDimensionNames.Length > 1 ? variableDimensionNames[1] : null;
        dimension3Name = variableDimensionNames.Length > 2 ? variableDimensionNames[2] : null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static SfInstrument Create(string name, KeyValuePair<string, object>[] fixedTags, string[] variableDimensionNames) =>
        new SfHistogramInstrument(SfMeterRegistry.GetOrCreateHistogram(name), fixedTags, variableDimensionNames);

    internal override void Record(long value)
    {
        if (!histogram.Enabled)
            return;

        // The fixed tags are the whole tag set of a meter without variable dimensions.
        Publish(value, fixedTags);
    }

    internal override void Record(long value, string dimension1Value)
    {
        if (!histogram.Enabled)
            return;

        KeyValuePair<string, object>[] cachedTags = GetCachedTags(dimension1Value, null, null, 1);
        if (cachedTags != null)
        {
            Publish(value, cachedTags);
            return;
        }

        TagList tags = new();
        AddFixedTags(ref tags);
        tags.Add(dimension1Name, FallbackTagValue(dimension1Name, dimension1Value));
        Publish(value, in tags);
    }

    internal override void Record(long value, string dimension1Value, string dimension2Value)
    {
        if (!histogram.Enabled)
            return;

        KeyValuePair<string, object>[] cachedTags = GetCachedTags(dimension1Value, dimension2Value, null, 2);
        if (cachedTags != null)
        {
            Publish(value, cachedTags);
            return;
        }

        TagList tags = new();
        AddFixedTags(ref tags);
        tags.Add(dimension1Name, FallbackTagValue(dimension1Name, dimension1Value));
        tags.Add(dimension2Name, FallbackTagValue(dimension2Name, dimension2Value));
        Publish(value, in tags);
    }

    internal override void Record(long value, string dimension1Value, string dimension2Value, string dimension3Value)
    {
        if (!histogram.Enabled)
            return;

        KeyValuePair<string, object>[] cachedTags = GetCachedTags(dimension1Value, dimension2Value, dimension3Value, 3);
        if (cachedTags != null)
        {
            Publish(value, cachedTags);
            return;
        }

        TagList tags = new();
        AddFixedTags(ref tags);
        tags.Add(dimension1Name, FallbackTagValue(dimension1Name, dimension1Value));
        tags.Add(dimension2Name, FallbackTagValue(dimension2Name, dimension2Value));
        tags.Add(dimension3Name, FallbackTagValue(dimension3Name, dimension3Value));
        Publish(value, in tags);
    }

    // Perf optimization: one cached tag array per dimension combination, recorded as a span (no TagList build or tag copy per
    // record). Callers pass the same string instances for the same values (cached enum and bool strings), so a reference match
    // is the common case.
    KeyValuePair<string, object>[] GetCachedTags(string dimension1Value, string dimension2Value, string dimension3Value, int dimensionCount)
    {
        TagSet[] sets = Volatile.Read(ref tagSets);
        for (int i = 0; i < sets.Length; i++)
        {
            TagSet set = sets[i];
            if (ReferenceEquals(set.Dimension1, dimension1Value) &&
                ReferenceEquals(set.Dimension2, dimension2Value) &&
                ReferenceEquals(set.Dimension3, dimension3Value))
                return set.Tags;
        }

        return GetOrAddTags(dimension1Value, dimension2Value, dimension3Value, dimensionCount);
    }

    // Equal values in other string instances share the entry: the SDK compares tag values by value, so the series is the same.
    // Returns null when the cache is full, without taking the lock.
    [MethodImpl(MethodImplOptions.NoInlining)]
    KeyValuePair<string, object>[] GetOrAddTags(string dimension1Value, string dimension2Value, string dimension3Value, int dimensionCount)
    {
        TagSet[] published = Volatile.Read(ref tagSets);
        KeyValuePair<string, object>[] tags = FindEqual(published, dimension1Value, dimension2Value, dimension3Value);
        if (tags != null || published.Length >= MaxCachedTagSets)
            return tags;

        lock (tagSetsLock)
        {
            TagSet[] sets = tagSets;
            tags = FindEqual(sets, dimension1Value, dimension2Value, dimension3Value);
            if (tags != null)
                return tags;
            if (sets.Length >= MaxCachedTagSets)
                return null;

            tags = new KeyValuePair<string, object>[fixedTags.Length + dimensionCount];
            Array.Copy(fixedTags, tags, fixedTags.Length);
            int index = fixedTags.Length;
            // Perf optimization: dimension values are SfTagValue, so the SDK does not hash the strings on every record.
            tags[index++] = new KeyValuePair<string, object>(dimension1Name, SfTagValue.For(dimension1Name, dimension1Value));
            if (dimensionCount > 1)
                tags[index++] = new KeyValuePair<string, object>(dimension2Name, SfTagValue.For(dimension2Name, dimension2Value));
            if (dimensionCount > 2)
                tags[index] = new KeyValuePair<string, object>(dimension3Name, SfTagValue.For(dimension3Name, dimension3Value));

            var newSets = new TagSet[sets.Length + 1];
            Array.Copy(sets, newSets, sets.Length);
            newSets[sets.Length] = new TagSet(dimension1Value, dimension2Value, dimension3Value, tags);
            Volatile.Write(ref tagSets, newSets);
            return tags;
        }
    }

    static KeyValuePair<string, object>[] FindEqual(TagSet[] sets, string dimension1Value, string dimension2Value, string dimension3Value)
    {
        for (int i = 0; i < sets.Length; i++)
        {
            TagSet set = sets[i];
            if (string.Equals(set.Dimension1, dimension1Value, StringComparison.Ordinal) &&
                string.Equals(set.Dimension2, dimension2Value, StringComparison.Ordinal) &&
                string.Equals(set.Dimension3, dimension3Value, StringComparison.Ordinal))
                return set.Tags;
        }

        return null;
    }

    void AddFixedTags(ref TagList tags)
    {
        KeyValuePair<string, object>[] fixedTags = this.fixedTags;
        for (int i = 0; i < fixedTags.Length; i++)
            tags.Add(fixedTags[i]);
    }

    // Perf optimization: the per-record path wraps a dimension value like the cached arrays do, so a combination is the same
    // series whichever path records it, and keeps one wrapper per value, so a warmed record neither hashes nor allocates.
    object FallbackTagValue(string dimensionName, string dimensionValue)
    {
        if (!SfTagValue.IsWrapped(dimensionName, dimensionValue))
            return dimensionValue;

        ConcurrentDictionary<string, SfTagValue> values = Volatile.Read(ref fallbackTagValues) ?? CreateFallbackTagValues();
        if (values.TryGetValue(dimensionValue, out SfTagValue tagValue))
            return tagValue;

        tagValue = new SfTagValue(dimensionValue);
        if (Volatile.Read(ref fallbackTagValuesFull) != 0)
            return tagValue;

        // Count takes every lock of the dictionary, but only a value seen for the first time gets here.
        if (values.Count >= MaxFallbackTagValues)
        {
            Volatile.Write(ref fallbackTagValuesFull, 1);
            return tagValue;
        }

        return values.GetOrAdd(dimensionValue, tagValue);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ConcurrentDictionary<string, SfTagValue> CreateFallbackTagValues()
    {
        _ = Interlocked.CompareExchange(ref fallbackTagValues, new ConcurrentDictionary<string, SfTagValue>(StringComparer.Ordinal), null);
        return fallbackTagValues;
    }

    void Publish(long value, KeyValuePair<string, object>[] tags)
    {
        try
        {
            histogram.Record(value, new ReadOnlySpan<KeyValuePair<string, object>>(tags));
        }
        catch (Exception e)
        {
            TraceRecordFailure(e);
        }
    }

    void Publish(long value, in TagList tags)
    {
        try
        {
            histogram.Record(value, in tags);
        }
        catch (Exception e)
        {
            TraceRecordFailure(e);
        }
    }

    // Listeners run inside Record. Like native MeterBase::RecordToInstrument, a failure never reaches the caller.
    void TraceRecordFailure(Exception e)
    {
        if (MetricsTrace.ShouldTraceOccurrence(ref recordFailureCount, out long count))
            MetricsTrace.Warning(RecordFailureTraceType, $"Failed to record metric '{histogram.Name}' (failure count: {count}). Error: {e}");
    }

    sealed class TagSet(string dimension1, string dimension2, string dimension3, KeyValuePair<string, object>[] tags)
    {
        internal readonly string Dimension1 = dimension1;
        internal readonly string Dimension2 = dimension2;
        internal readonly string Dimension3 = dimension3;
        internal readonly KeyValuePair<string, object>[] Tags = tags;
    }
}
