// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Tests that change the process-wide metrics backend, pipeline or trace sink. They must not run in parallel with other tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MetricsPipelineCollection
{
    internal const string Name = "Metrics pipeline";
}
