// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

public abstract class MetricsTraceTest : IDisposable
{
    readonly MetricsTestScope scope = new(withCollector: false);

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    void IDisposable.Dispose() => ((IDisposable)scope).Dispose();

    public sealed class Error : MetricsTraceTest
    {
        readonly string type = fuzzy.String();
        readonly string message = fuzzy.String();

        [Fact]
        public void WritesErrorTraceToSink()
        {
            MetricsTrace.Error(type, message);

            TraceRecord trace = Assert.Single(scope.TracesOfType(type));
            Assert.Equal(MetricsTraceLevel.Error, trace.Level);
            Assert.Same(message, trace.Message);
        }

        [Fact]
        public void IgnoresExceptionThrownBySink()
        {
            MetricsTrace.Sink = (_, _, _) => throw new InvalidOperationException();
            MetricsTrace.Error(type, message);
        }
    }
}
