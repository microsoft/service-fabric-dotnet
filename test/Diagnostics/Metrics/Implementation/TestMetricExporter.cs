// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Exporter standing in for the Geneva exporter; records what it was created with and every exported point.
/// </summary>
sealed class TestMetricExporter(string connectionString, IReadOnlyDictionary<string, object> prepopulatedDimensions) : BaseExporter<Metric>
{
    internal readonly string ConnectionString = connectionString;
    internal readonly IReadOnlyDictionary<string, object> PrepopulatedDimensions = prepopulatedDimensions;

    /// <summary>
    /// Runs at the start of every export, outside the exporter's lock; tests use it to hold an export open.
    /// </summary>
    internal volatile Action BeforeExport;

    /// <summary>
    /// Makes exports fail, like a collector that cannot be reached.
    /// </summary>
    internal volatile bool Fails;

    readonly object sync = new();
    readonly List<ExportedPoint> points = [];
    int exportCount;

    internal int ExportCount
    {
        get
        {
            lock (sync)
                return exportCount;
        }
    }

    internal IReadOnlyList<ExportedPoint> Points
    {
        get
        {
            lock (sync)
                return points.ToArray();
        }
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        BeforeExport?.Invoke();
        lock (sync)
        {
            exportCount++;
            foreach (Metric metric in batch)
                foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
                    points.Add(ExportedPoint.Capture(metric, in point));
        }

        return Fails ? ExportResult.Failure : ExportResult.Success;
    }
}
