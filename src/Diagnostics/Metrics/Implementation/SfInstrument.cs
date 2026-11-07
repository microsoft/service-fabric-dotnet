// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Recording target of an <see cref="OpenTelemetryInstrument"/>. It references this type instead of System.Diagnostics.Metrics
/// types, so meters load in a process where System.Diagnostics.DiagnosticSource cannot be loaded. The variable dimension values
/// passed to each overload match the dimension names the instrument was created with.
/// </summary>
abstract class SfInstrument
{
    internal abstract void Record(long value);

    internal abstract void Record(long value, string dimension1Value);

    internal abstract void Record(long value, string dimension1Value, string dimension2Value);

    internal abstract void Record(long value, string dimension1Value, string dimension2Value, string dimension3Value);
}
