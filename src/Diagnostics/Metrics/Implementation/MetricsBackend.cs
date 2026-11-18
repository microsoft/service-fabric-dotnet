// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Specifies the metrics backend of a process; the values of the Telemetry/Metrics ManagedMetricsBackend cluster setting.
/// </summary>
enum MetricsBackend
{
    /// <summary>
    /// Records meter values through COM interop with the native FabricTelemetry.dll, which exports them.
    /// </summary>
    ComInterop,

    /// <summary>
    /// Records meter values in process with System.Diagnostics.Metrics and exports them to Geneva with the OpenTelemetry SDK.
    /// </summary>
    OpenTelemetry,
}
