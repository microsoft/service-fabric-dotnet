// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Records the values of one meter in a metrics backend. The dimension values passed to each overload match the variable
/// dimension names the instrument was created with.
/// </summary>
interface IInstrument : IDisposable
{
    void Record(long value);

    void Record(long value, string dimension1Value);

    void Record(long value, string dimension1Value, string dimension2Value);

    void Record(long value, string dimension1Value, string dimension2Value, string dimension3Value);
}
