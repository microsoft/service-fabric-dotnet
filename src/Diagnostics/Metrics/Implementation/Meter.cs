// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation
{
    abstract class Meter : IDisposable
    {
        protected readonly IInstrument Instrument;

        internal Meter(IInstrument instrument)
        {
            Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        }

        public void Dispose() => Instrument.Dispose();

        protected void Record(long value) => Instrument.Record(value);

        protected long ConvertTimeSpanToLong(TimeSpan value) => (long)Math.Round(value.TotalMilliseconds);
    }
}
