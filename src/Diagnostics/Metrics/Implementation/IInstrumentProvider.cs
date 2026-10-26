// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Creates the instruments of one meter provider in a metrics backend. Every instrument records the fixed dimensions the
/// provider was created with, followed by its own variable dimensions.
/// </summary>
interface IInstrumentProvider : IDisposable
{
    IInstrument CreateInstrument(string metricNamespace, string name, string[] variableDimensionNames);
}
