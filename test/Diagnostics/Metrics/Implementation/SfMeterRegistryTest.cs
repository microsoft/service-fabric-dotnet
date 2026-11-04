// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System.Diagnostics.Metrics;
using System.Linq;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

public abstract class SfMeterRegistryTest
{
    public sealed class GetOrCreateHistogram : SfMeterRegistryTest
    {
        [Fact]
        public void PublishesOneInstrumentPerNameOnConcurrentFirstUse()
        {
            string name = MetricsTestScope.UniqueName("Namespace.Concurrent");

            Histogram<long>[] histograms = [.. Enumerable.Range(0, 64)
                .AsParallel()
                .WithDegreeOfParallelism(16)
                .Select(_ => SfMeterRegistry.GetOrCreateHistogram(name))];

            Assert.All(histograms, h => Assert.Same(histograms[0], h));
            Assert.Same(SfMeterRegistry.Meter, histograms[0].Meter);
        }
    }
}
