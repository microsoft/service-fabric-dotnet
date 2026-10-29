// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Inspector;
using Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;
using Moq;

namespace Microsoft.ServiceFabric
{
    public class FabricTelemetryDllFixture : IDisposable
    {
        readonly Lazy<MetricsBackend> backend = typeof(MetricsBackendSelector).Field<Lazy<MetricsBackend>>().Value;

        public FabricTelemetryDllFixture()
        {
            typeof(MetricsBackendSelector).Field<Lazy<MetricsBackend>>().Set(new Lazy<MetricsBackend>(() => MetricsBackend.ComInterop));
            typeof(ComInstrumentProvider).Field<Func<IFabricMeterProvider2>>().Set(() => new Mock<IFabricMeterProvider2>() { DefaultValue = DefaultValue.Mock }.Object);
        }

        public virtual void Dispose()
        {
            typeof(ComInstrumentProvider).Field<Func<IFabricMeterProvider2>>().Set(NativeTelemetry.FabricCreateMeterProvider);
            typeof(MetricsBackendSelector).Field<Lazy<MetricsBackend>>().Set(backend);
        }
    }
}
