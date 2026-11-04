// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenTelemetry.Metrics;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// One exported histogram point, copied during export because the SDK reuses its metric points.
/// </summary>
sealed class ExportedPoint(
    string metricName,
    string meterName,
    string meterVersion,
    IReadOnlyDictionary<string, object> tags,
    long count,
    double sum,
    bool hasMinMax,
    double min,
    double max,
    IReadOnlyList<KeyValuePair<double, long>> buckets)
{
    internal readonly string MetricName = metricName;
    internal readonly string MeterName = meterName;
    internal readonly string MeterVersion = meterVersion;
    internal readonly IReadOnlyDictionary<string, object> Tags = tags;
    internal readonly long Count = count;
    internal readonly double Sum = sum;
    internal readonly bool HasMinMax = hasMinMax;
    internal readonly double Min = min;
    internal readonly double Max = max;
    internal readonly IReadOnlyList<KeyValuePair<double, long>> Buckets = buckets;

    internal static ExportedPoint Capture(Metric metric, in MetricPoint point)
    {
        Dictionary<string, object> tags = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object> tag in point.Tags)
            tags[tag.Key] = tag.Value;

        List<KeyValuePair<double, long>> buckets = [];
        foreach (HistogramBucket bucket in point.GetHistogramBuckets())
            buckets.Add(new KeyValuePair<double, long>(bucket.ExplicitBound, bucket.BucketCount));

        bool hasMinMax = point.TryGetHistogramMinMaxValues(out double min, out double max);

        return new ExportedPoint(
            metric.Name, metric.MeterName, metric.MeterVersion, tags, point.GetHistogramCount(), point.GetHistogramSum(), hasMinMax, min, max, buckets);
    }

    internal long BucketCount(double explicitBound) => Buckets.Single(b => b.Key.Equals(explicitBound)).Value;

    // The string the Geneva exporter writes for the tag (TlvMetricExporter.ConvertTagValueToString); values are SfTagValue.
    internal string Tag(string name) => Convert.ToString(Tags[name], CultureInfo.InvariantCulture);
}
