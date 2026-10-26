// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

sealed class TraceRecord(MetricsTraceLevel level, string type, string message)
{
    internal readonly MetricsTraceLevel Level = level;
    internal readonly string Type = type;
    internal readonly string Message = message;

    public override string ToString() => $"{Level} {Type}: {Message}";
}
