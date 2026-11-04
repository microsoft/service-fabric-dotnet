// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Metrics backend of a process; the values of the Telemetry/Metrics ManagedMetricsBackend cluster setting.
/// </summary>
enum MetricsBackend
{
    /// <summary>
    /// Meters record through COM interop with the native FabricTelemetry.dll, which exports the values.
    /// </summary>
    ComInterop,

    /// <summary>
    /// Meters record in process with System.Diagnostics.Metrics, and the OpenTelemetry SDK exports the values to Geneva.
    /// </summary>
    OpenTelemetry,
}
