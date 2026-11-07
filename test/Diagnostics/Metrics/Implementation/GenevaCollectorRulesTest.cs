// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

[Collection(MetricsPipelineCollection.Name)]
public abstract class GenevaCollectorRulesTest : IDisposable
{
    readonly MetricsTestScope scope = new(withCollector: false);

    void IDisposable.Dispose() => scope.Dispose();

    public sealed class IsValidConnectionString : GenevaCollectorRulesTest
    {
        [Theory]
        [InlineData("Account=a;Namespace=n")]
        [InlineData("Namespace=n;Account=a;")]
        [InlineData("Account=a;NoEquals;Namespace=n;Other=x")]
        public void ReturnsTrueOnWindowsForAccountAndNamespaceWithoutEndpoint(string connectionString) =>
            Assert.True(GenevaCollectorRules.IsValidConnectionString(connectionString, isLinux: false));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Account=a")]
        [InlineData("Namespace=n")]
        [InlineData("Account=;Namespace=n")]
        [InlineData("account=a;namespace=n")] // Keys are case-sensitive.
        [InlineData("Endpoint=unix:///var/etw/mdm_ifx.socket;Account=a;Namespace=n")]
        public void ReturnsFalseOnWindowsWithoutAccountAndNamespaceOrWithEndpoint(string connectionString) =>
            Assert.False(GenevaCollectorRules.IsValidConnectionString(connectionString, isLinux: false));

        [Theory]
        [InlineData("Endpoint=unix:///var/etw/mdm_ifx.socket;Account=a;Namespace=n")]
        [InlineData("Endpoint=unix:///var/etw/mdm_ifx.socket")] // Native does not require Account and Namespace on Linux.
        [InlineData("Endpoint=unix://@abstract")]
        public void ReturnsTrueOnLinuxForUnixEndpoint(string connectionString) =>
            Assert.True(GenevaCollectorRules.IsValidConnectionString(connectionString, isLinux: true));

        [Theory]
        [InlineData("Account=a;Namespace=n")]
        [InlineData("Endpoint=tcp://localhost:1234;Account=a;Namespace=n")]
        [InlineData("Endpoint=unix:@abstract")] // Native requires the scheme separator '://'.
        [InlineData("endpoint=unix:///var/etw/mdm_ifx.socket")]
        public void ReturnsFalseOnLinuxWithoutUnixEndpoint(string connectionString) =>
            Assert.False(GenevaCollectorRules.IsValidConnectionString(connectionString, isLinux: true));
    }

    public sealed class FilterPrepopulatedDimensions : GenevaCollectorRulesTest
    {
        [Fact]
        public void DropsAndTracesReservedNamesIgnoringCase()
        {
            IReadOnlyDictionary<string, object> result = GenevaCollectorRules.FilterPrepopulatedDimensions(new Dictionary<string, string>
            {
                { "_microsoft_metrics_account", "a" },
                { "_MICROSOFT_METRICS_NAMESPACE", "n" },
                { "Kept", "v" },
            });

            Assert.Equal(["Kept"], result.Keys);
            Assert.Equal(2, scope.TracesOfType("OpenTelemetryMetricsFactory").Count);
        }

        [Fact]
        public void EnforcesExporterLengthLimitsInUtf16CodeUnits()
        {
            string name256 = new('n', 256);
            string name257 = new('n', 257);

            // Two UTF-8 bytes per character; within the limit because the exporter counts UTF-16 code units.
            string value1024Multibyte = new('\u00e9', 1024);

            // A surrogate pair is two UTF-16 code units.
            string value1025WithSurrogatePair = new string('v', 1023) + "\ud83d\ude00";

            IReadOnlyDictionary<string, object> result = GenevaCollectorRules.FilterPrepopulatedDimensions(new Dictionary<string, string>
            {
                { name256, "v" },
                { name257, "v" },
                { "Value1024", value1024Multibyte },
                { "Value1025", value1025WithSurrogatePair },
            });

            Assert.Contains(name256, result);
            Assert.DoesNotContain(name257, result);
            Assert.Equal(value1024Multibyte, result["Value1024"]);
            Assert.DoesNotContain("Value1025", result);
        }

        [Fact]
        public void ConvertsNullValueToEmpty()
        {
            IReadOnlyDictionary<string, object> result = GenevaCollectorRules.FilterPrepopulatedDimensions(new Dictionary<string, string> { { "Name", null } });

            Assert.Equal(string.Empty, result["Name"]);
        }
    }
}
