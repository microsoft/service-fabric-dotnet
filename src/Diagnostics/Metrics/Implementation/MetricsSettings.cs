// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Metrics settings of managed meter providers. Port of native MetricsConfig and MetricsSettings (FabricTelemetry,
/// src/prod/src/Telemetry/Native/config in the WindowsFabric repo); keep the two in sync.
/// </summary>
sealed class MetricsSettings
{
    internal const string MetricsSection = "Telemetry/Metrics";
    internal const string DimensionsSection = "Telemetry/Metrics/Dimensions";
    internal const string CollectorsSection = "Telemetry/Metrics/Collectors";
    internal const string FabricNodeSection = "FabricNode";

    internal const string NodeNameDimension = "NodeName";
    internal const string RuntimeVersionDimension = "RuntimeVersion";
    internal const string OsTypeDimension = "OsType";
    internal const string ArchitectureDimension = "Architecture";
    internal const string VmssIdDimension = "Microsoft.ResourceId";
    internal const string UnknownVmssId = "Unknown";

    const string TraceType = "MetricsSettings";
    const string EnvPrefix = "env:";
    const string ResourceIdDimensionKey = "ResourceId";
    const string GenevaCollectorName = "Geneva";
    const ushort ImageFileMachineArm64 = 0xAA64;

    static readonly IReadOnlyDictionary<string, string> noDimensions = new SortedDictionary<string, string>(StringComparer.Ordinal);
    static readonly IReadOnlyList<string> noConnectionStrings = [];

    /// <summary>
    /// Telemetry/Metrics IsEnabled (default true).
    /// </summary>
    internal readonly bool IsEnabled;

    /// <summary>
    /// Telemetry/Metrics IsManagedTelemetryEnabled (default true).
    /// </summary>
    internal readonly bool IsManagedTelemetryEnabled;

    /// <summary>
    /// Dimensions added to every exported metric, ordered by name like the native std::map. Empty when the gate is closed.
    /// </summary>
    internal readonly IReadOnlyDictionary<string, string> Dimensions;

    /// <summary>
    /// Connection strings of the Geneva collector, before validation. Empty when the gate is closed.
    /// </summary>
    internal readonly IReadOnlyList<string> GenevaConnectionStrings;

    MetricsSettings(
        bool isEnabled,
        bool isManagedTelemetryEnabled,
        IReadOnlyDictionary<string, string> dimensions,
        IReadOnlyList<string> genevaConnectionStrings)
    {
        IsEnabled = isEnabled;
        IsManagedTelemetryEnabled = isManagedTelemetryEnabled;
        Dimensions = dimensions;
        GenevaConnectionStrings = genevaConnectionStrings;
    }

    /// <summary>
    /// Gets a value that indicates whether managed meter providers record: the same gate as native MeterProvider's for managed
    /// telemetry providers.
    /// </summary>
    internal bool IsMetricsEnabled => IsEnabled && IsManagedTelemetryEnabled;

    internal static MetricsSettings Load() => Load(new RuntimeMetricsSettingsSource());

    /// <summary>
    /// Reads the gate and, when it is open, the dimensions and collectors. Throws when the configuration cannot be read.
    /// </summary>
    internal static MetricsSettings Load(IMetricsSettingsSource source)
    {
        bool isEnabled = ReadBool(source, MetricsSection, "IsEnabled", defaultValue: true);

        // Like native LoadMetricsSettings: disabled settings report both flags false.
        if (!isEnabled)
            return new MetricsSettings(false, false, noDimensions, noConnectionStrings);

        bool isManagedTelemetryEnabled = ReadBool(source, MetricsSection, "IsManagedTelemetryEnabled", defaultValue: true);
        if (!isManagedTelemetryEnabled)
            return new MetricsSettings(true, false, noDimensions, noConnectionStrings);

        SortedDictionary<string, string> dimensions = new(StringComparer.Ordinal);
        foreach (string dimensionName in source.GetConfigKeys(DimensionsSection))
        {
            string value = source.ReadConfigValue(DimensionsSection, dimensionName) ?? string.Empty;
            if (value.StartsWith(EnvPrefix, StringComparison.Ordinal))
            {
                string variableName = value.Substring(EnvPrefix.Length);
                value = source.GetEnvironmentVariable(variableName);
                if (value == null)
                {
                    // Native throws here and metrics fail to initialize; managed exports an empty value instead.
                    MetricsTrace.Warning(
                        TraceType,
                        $"Environment variable '{variableName}' of dimension '{dimensionName}' is not set; the dimension value is empty.");
                    value = string.Empty;
                }
            }

            dimensions[dimensionName] = value;
        }

        dimensions[NodeNameDimension] = GetNodeName(source);
        dimensions[RuntimeVersionDimension] = GetRuntimeVersion(source);
        dimensions[OsTypeDimension] = GetOsType();
        dimensions[ArchitectureDimension] = GetArchitecture();

        string vmssId = GetVmssId(dimensions, source);
        if (vmssId.Length > 0)
        {
            dimensions[VmssIdDimension] = vmssId;
        }
        else
        {
            dimensions[VmssIdDimension] = UnknownVmssId;
            MetricsTrace.Info(TraceType, "VMSS ARM ID not found; setting VmssId dimension to Unknown.");
        }

        return new MetricsSettings(true, true, dimensions, ReadGenevaConnectionStrings(source));
    }

    /// <summary>
    /// Parses a FabricVersionInstance ("major.minor.build.private:configVersion:instanceId") like native FabricVersionInstance::FromString.
    /// Returns false for an absent or malformed value and for the default instance, where native falls back to the installed version.
    /// </summary>
    internal static bool TryGetCodeVersion(string fabricVersionInstance, out string codeVersion)
    {
        codeVersion = null;
        if (string.IsNullOrEmpty(fabricVersionInstance))
            return false;

        string[] tokens = fabricVersionInstance.Split(':');
        if (tokens.Length != 3 || !ulong.TryParse(tokens[2], NumberStyles.None, CultureInfo.InvariantCulture, out ulong instanceId))
            return false;

        string[] parts = tokens[0].Split('.');
        var numbers = new uint[4];
        if (parts.Length != 4)
            return false;

        for (int i = 0; i < 4; i++)
            if (!uint.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;

        // FabricVersionInstance::Default is code version 1.0.960.0, config version "__default__", instance 0.
        bool isDefault = numbers[0] == 1 && numbers[1] == 0 && numbers[2] == 960 && numbers[3] == 0 && tokens[1] == "__default__" && instanceId == 0;
        if (isDefault)
            return false;

        codeVersion = $"{numbers[0]}.{numbers[1]}.{numbers[2]}.{numbers[3]}";
        return true;
    }

    /// <summary>
    /// Port of native MetricsSettings::GetArchitecture, which reports the architecture FabricTelemetry.dll was compiled
    /// for. On ARM64 hosts that DLL is ARM64X, so an x64 process runs its ARM64EC code and reports "ARM64EC".
    /// </summary>
    internal static string GetArchitecture(Architecture processArchitecture, bool isArm64Host) =>
        processArchitecture switch
        {
            Architecture.X64 => isArm64Host ? "ARM64EC" : "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "ARM64",
            Architecture.Arm => "ARM",
            _ => "Unknown",
        };

    /// <summary>
    /// Native config parses a present bool value as true only when it equals "true" ignoring case; an absent value is the default.
    /// </summary>
    static bool ReadBool(IMetricsSettingsSource source, string sectionName, string keyName, bool defaultValue)
    {
        string value = source.ReadConfigValue(sectionName, keyName);
        return string.IsNullOrEmpty(value) ? defaultValue : string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    static IReadOnlyList<string> ReadGenevaConnectionStrings(IMetricsSettingsSource source)
    {
        foreach (string collectorName in source.GetConfigKeys(CollectorsSection))
        {
            if (collectorName != GenevaCollectorName)
                continue;

            string json = source.ReadConfigValue(CollectorsSection, collectorName);
            if (!GenevaCollectorSettingsJson.TryParse(json, out IReadOnlyList<string> connectionStrings, out Exception error))
            {
                MetricsTrace.Warning(TraceType, $"Unable to deserialize Geneva collector settings. Error = {error}");
                return noConnectionStrings;
            }

            return connectionStrings;
        }

        return noConnectionStrings;
    }

    static string GetNodeName(IMetricsSettingsSource source)
    {
        string instanceName = source.ReadConfigValue(FabricNodeSection, "InstanceName");
        return string.IsNullOrEmpty(instanceName) ? source.GetHostName() : instanceName;
    }

    static string GetRuntimeVersion(IMetricsSettingsSource source)
    {
        string nodeVersion = source.ReadConfigValue(FabricNodeSection, "NodeVersion");
        return TryGetCodeVersion(nodeVersion, out string codeVersion) ? codeVersion : source.GetFabricVersion();
    }

    static string GetOsType() => GenevaCollectorRules.IsLinux ? "Linux" : "Windows";

    static string GetArchitecture()
    {
        Architecture processArchitecture = RuntimeInformation.ProcessArchitecture;
        return GetArchitecture(processArchitecture, processArchitecture == Architecture.X64 && IsArm64WindowsHost());
    }

    static bool IsArm64WindowsHost()
    {
        if (GenevaCollectorRules.IsLinux)
            return false;

        try
        {
            return IsWow64Process2(GetCurrentProcess(), out ushort _, out ushort nativeMachine) && nativeMachine == ImageFileMachineArm64;
        }
        catch (Exception)
        {
            // IsWow64Process2 needs Windows 10 1709 or later; Windows without it has no ARM64 hosts.
            return false;
        }
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);

    /// <summary>
    /// Port of native MetricsSettings::GetVmssId: VMSS_ARM_ID when set, otherwise the scale set ID derived from the ResourceId
    /// dimension and FabricNode/NodeType (SFRP names the scale set after the node type). Returns "" when it cannot be derived.
    /// </summary>
    static string GetVmssId(SortedDictionary<string, string> dimensions, IMetricsSettingsSource source)
    {
        string vmssArmId = source.GetEnvironmentVariable("VMSS_ARM_ID")?.Trim();
        if (!string.IsNullOrEmpty(vmssArmId))
        {
            MetricsTrace.Info(TraceType, $"GetVmssId: using VMSS_ARM_ID environment variable override: {vmssArmId}");
            return vmssArmId;
        }

        // Case-insensitive lookup; the first match in name order wins, as with the native std::map.
        string resourceId = null;
        foreach (KeyValuePair<string, string> dimension in dimensions)
        {
            if (string.Equals(dimension.Key, ResourceIdDimensionKey, StringComparison.OrdinalIgnoreCase))
            {
                resourceId = dimension.Value;
                break;
            }
        }

        if (string.IsNullOrEmpty(resourceId))
        {
            MetricsTrace.Warning(
                TraceType,
                $"GetVmssId: ResourceId dimension not found or empty in config; cannot derive VmssId. Dimension keys present: {string.Join(", ", dimensions.Keys)}");
            return string.Empty;
        }

        if (resourceId[0] != '/')
            resourceId = "/" + resourceId;

        string resourceIdLower = resourceId.ToLowerInvariant();
        const string SubscriptionsToken = "/subscriptions/";
        const string ResourceGroupsToken = "/resourcegroups/";

        int subscriptionStart = resourceIdLower.IndexOf(SubscriptionsToken, StringComparison.Ordinal);
        if (subscriptionStart < 0)
        {
            MetricsTrace.Warning(TraceType, $"ResourceId does not contain /subscriptions/ segment: {resourceId}");
            return string.Empty;
        }

        subscriptionStart += SubscriptionsToken.Length;
        int subscriptionEnd = resourceIdLower.IndexOf('/', subscriptionStart);
        if (subscriptionEnd < 0)
        {
            MetricsTrace.Warning(TraceType, $"ResourceId has no segment after subscription: {resourceId}");
            return string.Empty;
        }

        string subscriptionId = resourceId.Substring(subscriptionStart, subscriptionEnd - subscriptionStart);

        int resourceGroupStart = resourceIdLower.IndexOf(ResourceGroupsToken, StringComparison.Ordinal);
        if (resourceGroupStart < 0)
        {
            MetricsTrace.Warning(TraceType, $"ResourceId does not contain /resourceGroups/ segment: {resourceId}");
            return string.Empty;
        }

        resourceGroupStart += ResourceGroupsToken.Length;
        int resourceGroupEnd = resourceIdLower.IndexOf('/', resourceGroupStart);
        if (resourceGroupEnd < 0)
            resourceGroupEnd = resourceId.Length;

        string resourceGroupName = resourceId.Substring(resourceGroupStart, resourceGroupEnd - resourceGroupStart);

        string nodeType = source.ReadConfigValue(FabricNodeSection, "NodeType");
        if (string.IsNullOrEmpty(nodeType))
        {
            MetricsTrace.Warning(TraceType, "NodeType is empty in FabricNodeConfig; cannot derive VmssId.");
            return string.Empty;
        }

        vmssArmId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{nodeType}";
        MetricsTrace.Info(TraceType, $"Derived VmssId from config: {vmssArmId}");
        return vmssArmId;
    }
}
