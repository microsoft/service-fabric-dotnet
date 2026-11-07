// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation
{
    sealed class Int64Meter3D : Meter3D, IMeter3D<long>
    {
        internal Int64Meter3D(IInstrument instrument) : base(instrument) { }

        void IMeter3D<long>.Record(long value, string dimension1Value, string dimension2Value, string dimension3Value)
        {
            base.Record(value, dimension1Value, dimension2Value, dimension3Value);
        }
    }
}
