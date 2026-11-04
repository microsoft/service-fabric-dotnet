// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Port of the native MetricsSettings tests in the WindowsFabric repo, plus the managed rules.
/// </summary>
[Collection(MetricsPipelineCollection.Name)]
public abstract class MetricsSettingsTest : IDisposable
{
    const string Dimensions = "Telemetry/Metrics/Dimensions";

    // Captures traces; the settings themselves are loaded directly from a fake source.
    readonly MetricsTestScope scope = new(withCollector: false);

    void IDisposable.Dispose() => scope.Dispose();

    public sealed class Load : MetricsSettingsTest
    {
        const string ArmId = "/subscriptions/sub-123/resourceGroups/rg-abc/providers/Microsoft.Compute/virtualMachines/vm0";
        const string DerivedVmssId = "/subscriptions/sub-123/resourceGroups/rg-abc/providers/Microsoft.Compute/virtualMachineScaleSets/nt0";

        [Fact]
        public void ReturnsFiveAutomaticDimensionsByDefault()
        {
            MetricsSettings settings = MetricsSettings.Load(new FakeMetricsSettingsSource());

            Assert.True(settings.IsEnabled);
            Assert.True(settings.IsManagedTelemetryEnabled);
            Assert.Equal(["Architecture", "Microsoft.ResourceId", "NodeName", "OsType", "RuntimeVersion"], settings.Dimensions.Keys);
        }

        [Fact]
        public void ReturnsNativeArchitectureDimension()
        {
            MetricsSettings settings = MetricsSettings.Load(new FakeMetricsSettingsSource());

            Assert.Contains(settings.Dimensions["Architecture"], new[] { "x64", "x86", "ARM64", "ARM64EC", "ARM" });
        }

        [Fact]
        public void ResolvesEnvironmentVariablesOfExtraDimensions()
        {
            FakeMetricsSettingsSource source = new FakeMetricsSettingsSource()
                .Set(Dimensions, "Dimension1Key", "Dimension1Value")
                .Set(Dimensions, "Dimension2Key", "Dimension2Value")
                .Set(Dimensions, "Dimension3Key", "env:COMPUTERNAME")
                .SetEnvironmentVariable("COMPUTERNAME", "TestComputer");

            MetricsSettings settings = MetricsSettings.Load(source);

            Assert.Equal(8, settings.Dimensions.Count);
            Assert.Equal("Dimension1Value", settings.Dimensions["Dimension1Key"]);
            Assert.Equal("Dimension2Value", settings.Dimensions["Dimension2Key"]);
            Assert.Equal("TestComputer", settings.Dimensions["Dimension3Key"]);
        }

        [Fact]
        public void ReturnsEmptyDimensionValueAndTracesWarningForMissingEnvironmentVariable()
        {
            MetricsSettings settings = MetricsSettings.Load(new FakeMetricsSettingsSource().Set(Dimensions, "Region", "env:NOT_SET"));

            Assert.Equal(string.Empty, settings.Dimensions["Region"]);
            Assert.Contains(scope.TracesOfType("MetricsSettings"), t => t.Level == MetricsTraceLevel.Warning && t.Message.Contains("NOT_SET"));
        }

        [Fact]
        public void MatchesEnvPrefixCaseSensitively()
        {
            MetricsSettings settings = MetricsSettings.Load(new FakeMetricsSettingsSource().Set(Dimensions, "Region", "ENV:X").SetEnvironmentVariable("X", "y"));

            Assert.Equal("ENV:X", settings.Dimensions["Region"]);
        }

        [Fact]
        public void OverridesConfiguredDimensionsWithAutomaticOnes()
        {
            MetricsSettings settings = MetricsSettings.Load(new FakeMetricsSettingsSource().Set(Dimensions, "NodeName", "Configured").Set(Dimensions, "OsType", "Configured"));

            Assert.Equal(FakeMetricsSettingsSource.HostName, settings.Dimensions["NodeName"]);
            Assert.NotEqual("Configured", settings.Dimensions["OsType"]);
        }

        [Fact]
        public void ReturnsInstanceNameOrHostNameAsNodeName()
        {
            Assert.Equal(FakeMetricsSettingsSource.HostName, MetricsSettings.Load(new FakeMetricsSettingsSource()).Dimensions["NodeName"]);
            Assert.Equal("_Node_1", MetricsSettings.Load(new FakeMetricsSettingsSource().Set("FabricNode", "InstanceName", "_Node_1")).Dimensions["NodeName"]);
        }

        [Fact]
        public void ReturnsCodeVersionOfNodeVersionOrInstalledVersionAsRuntimeVersion()
        {
            Assert.Equal(FakeMetricsSettingsSource.FabricVersion, MetricsSettings.Load(new FakeMetricsSettingsSource()).Dimensions["RuntimeVersion"]);
            Assert.Equal(
                "12.0.1234.5",
                MetricsSettings.Load(new FakeMetricsSettingsSource().Set("FabricNode", "NodeVersion", "12.0.1234.5:1.0:133")).Dimensions["RuntimeVersion"]);
            Assert.Equal(
                FakeMetricsSettingsSource.FabricVersion,
                MetricsSettings.Load(new FakeMetricsSettingsSource().Set("FabricNode", "NodeVersion", "1.0.960.0:__default__:0")).Dimensions["RuntimeVersion"]);
            Assert.Equal(
                FakeMetricsSettingsSource.FabricVersion,
                MetricsSettings.Load(new FakeMetricsSettingsSource().Set("FabricNode", "NodeVersion", "not-a-version")).Dimensions["RuntimeVersion"]);
        }

        [Fact]
        public void ReturnsNoConnectionStringsByDefault() =>
            Assert.Empty(MetricsSettings.Load(new FakeMetricsSettingsSource()).GenevaConnectionStrings);

        [Fact]
        public void ReturnsConnectionStringsOfGenevaCollector()
        {
            MetricsSettings settings = MetricsSettings.Load(new FakeMetricsSettingsSource().SetCollectors("connectionString1", "connectionString2"));

            Assert.Equal(["connectionString1", "connectionString2"], settings.GenevaConnectionStrings);
        }

        [Fact]
        public void ReturnsNoConnectionStringsAndTracesWarningForInvalidGenevaCollector()
        {
            FakeMetricsSettingsSource source = new FakeMetricsSettingsSource().Set("Telemetry/Metrics/Collectors", "Geneva", "{\"Not Valid JSON\"}");

            Assert.Empty(MetricsSettings.Load(source).GenevaConnectionStrings);
            Assert.Contains(scope.TracesOfType("MetricsSettings"), t => t.Message.Contains("Unable to deserialize Geneva collector settings"));
        }

        [Fact]
        public void MatchesCollectorNamesAndJsonPropertiesCaseSensitively()
        {
            Assert.Empty(MetricsSettings.Load(new FakeMetricsSettingsSource().Set("Telemetry/Metrics/Collectors", "geneva", "{\"ConnectionStrings\":[\"a\"]}")).GenevaConnectionStrings);
            Assert.Empty(MetricsSettings.Load(new FakeMetricsSettingsSource().Set("Telemetry/Metrics/Collectors", "Geneva", "{\"connectionStrings\":[\"a\"]}")).GenevaConnectionStrings);
        }

        [Fact]
        public void ReturnsDisabledSettingsWhenIsEnabledIsFalse()
        {
            MetricsSettings settings = MetricsSettings.Load(new FakeMetricsSettingsSource().Set("Telemetry/Metrics", "IsEnabled", "false").SetCollectors("a"));

            Assert.False(settings.IsEnabled);
            Assert.False(settings.IsManagedTelemetryEnabled);
            Assert.Empty(settings.Dimensions);
            Assert.Empty(settings.GenevaConnectionStrings);
        }

        [Fact]
        public void ClosesGateWhenIsManagedTelemetryEnabledIsFalse()
        {
            MetricsSettings settings = MetricsSettings.Load(new FakeMetricsSettingsSource().Set("Telemetry/Metrics", "IsManagedTelemetryEnabled", "false"));

            Assert.True(settings.IsEnabled);
            Assert.False(settings.IsMetricsEnabled);
        }

        [Fact]
        public void ReturnsUnknownVmssIdWithoutResourceId() =>
            Assert.Equal("Unknown", LoadVmssId(new FakeMetricsSettingsSource()));

        [Fact]
        public void ReturnsUnknownVmssIdForMalformedResourceId() =>
            Assert.Equal("Unknown", LoadVmssId(new FakeMetricsSettingsSource().Set(Dimensions, "ResourceId", "not-an-arm-id")));

        [Fact]
        public void DerivesVmssIdFromResourceIdAndNodeType() =>
            Assert.Equal(DerivedVmssId, LoadVmssId(WithNodeType().Set(Dimensions, "ResourceId", ArmId)));

        [Fact]
        public void DerivesVmssIdFromCaseInsensitiveDimensionKey() =>
            Assert.Equal(DerivedVmssId, LoadVmssId(WithNodeType().Set(Dimensions, "resourceid", ArmId)));

        [Fact]
        public void DerivesVmssIdFromCaseInsensitiveArmSegments() =>
            Assert.Equal(
                DerivedVmssId,
                LoadVmssId(WithNodeType().Set(Dimensions, "ResourceId", "/SUBSCRIPTIONS/sub-123/RESOURCEGROUPS/rg-abc/providers/Microsoft.Compute/virtualMachines/vm0")));

        [Fact]
        public void DerivesVmssIdFromResourceIdWithoutLeadingSlash() =>
            Assert.Equal(DerivedVmssId, LoadVmssId(WithNodeType().Set(Dimensions, "ResourceId", ArmId.Substring(1))));

        [Fact]
        public void DerivesVmssIdFromResourceIdEndingWithResourceGroup() =>
            Assert.Equal(DerivedVmssId, LoadVmssId(WithNodeType().Set(Dimensions, "ResourceId", "/subscriptions/sub-123/resourceGroups/rg-abc")));

        [Fact]
        public void ReturnsUnknownVmssIdWithoutSubscriptionsSegment() =>
            Assert.Equal(
                "Unknown",
                LoadVmssId(WithNodeType().Set(Dimensions, "ResourceId", "/resourceGroups/rg-abc/providers/Microsoft.Compute/virtualMachines/vm0")));

        [Fact]
        public void ReturnsUnknownVmssIdWithoutSegmentAfterSubscription() =>
            Assert.Equal("Unknown", LoadVmssId(WithNodeType().Set(Dimensions, "ResourceId", "/subscriptions/sub-123")));

        [Fact]
        public void ReturnsUnknownVmssIdWithoutResourceGroupsSegment() =>
            Assert.Equal(
                "Unknown",
                LoadVmssId(WithNodeType().Set(Dimensions, "ResourceId", "/subscriptions/sub-123/providers/Microsoft.Compute/virtualMachines/vm0")));

        [Fact]
        public void ReturnsUnknownVmssIdWithoutNodeType() =>
            Assert.Equal("Unknown", LoadVmssId(new FakeMetricsSettingsSource().Set(Dimensions, "ResourceId", ArmId)));

        [Fact]
        public void PreservesOriginalCasingInDerivedVmssId()
        {
            FakeMetricsSettingsSource source = new FakeMetricsSettingsSource()
                .Set("FabricNode", "NodeType", "PrimaryNodeType")
                .Set(Dimensions, "ResourceId", "/subscriptions/MySubscription-ABC/resourceGroups/MyResourceGroup-XYZ/providers/Microsoft.Compute/virtualMachines/vm0");

            Assert.Equal(
                "/subscriptions/MySubscription-ABC/resourceGroups/MyResourceGroup-XYZ/providers/Microsoft.Compute/virtualMachineScaleSets/PrimaryNodeType",
                LoadVmssId(source));
        }

        [Fact]
        public void UsesTrimmedVmssArmIdEnvironmentVariableOverDerivedVmssId()
        {
            const string Override = "/subscriptions/env-sub/resourceGroups/env-rg/providers/Microsoft.Compute/virtualMachineScaleSets/env-vmss";
            FakeMetricsSettingsSource source = WithNodeType().Set(Dimensions, "ResourceId", ArmId).SetEnvironmentVariable("VMSS_ARM_ID", "  " + Override + " ");

            Assert.Equal(Override, LoadVmssId(source));
        }

        [Fact]
        public void DerivesVmssIdWhenVmssArmIdEnvironmentVariableIsWhitespace()
        {
            FakeMetricsSettingsSource source = WithNodeType().Set(Dimensions, "ResourceId", ArmId).SetEnvironmentVariable("VMSS_ARM_ID", "   ");

            Assert.Equal(DerivedVmssId, LoadVmssId(source));
        }

        static FakeMetricsSettingsSource WithNodeType() => new FakeMetricsSettingsSource().Set("FabricNode", "NodeType", "nt0");

        static string LoadVmssId(FakeMetricsSettingsSource source) => MetricsSettings.Load(source).Dimensions["Microsoft.ResourceId"];
    }

    public sealed class GetArchitecture : MetricsSettingsTest
    {
        [Theory]
        [InlineData(Architecture.X64, false, "x64")]
        [InlineData(Architecture.X64, true, "ARM64EC")]
        [InlineData(Architecture.X86, false, "x86")]
        [InlineData(Architecture.Arm64, true, "ARM64")]
        [InlineData(Architecture.Arm, false, "ARM")]
        [InlineData((Architecture)99, false, "Unknown")]
        public void MapsArchitectureLikeNative(Architecture processArchitecture, bool isArm64Host, string expected) =>
            Assert.Equal(expected, MetricsSettings.GetArchitecture(processArchitecture, isArm64Host));
    }

    public sealed class TryGetCodeVersion : MetricsSettingsTest
    {
        [Theory]
        [InlineData("12.0.1234.5:cfg:7", "12.0.1234.5")]
        [InlineData("012.00.1.2:cfg:0", "12.0.1.2")]
        public void ReturnsTrueAndCodeVersionLikeNativeFabricVersionInstance(string fabricVersionInstance, string expected)
        {
            Assert.True(MetricsSettings.TryGetCodeVersion(fabricVersionInstance, out string codeVersion));
            Assert.Equal(expected, codeVersion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("12.0.1234.5")]
        [InlineData("12.0.1234.5:cfg")]
        [InlineData("12.0.1234:cfg:7")]
        [InlineData("12.0.1234.5:cfg:-1")]
        [InlineData("12.0.x.5:cfg:7")]
        [InlineData("1.0.960.0:__default__:0")]
        public void ReturnsFalseForAbsentMalformedOrDefaultInstance(string fabricVersionInstance) =>
            Assert.False(MetricsSettings.TryGetCodeVersion(fabricVersionInstance, out _));
    }
}
