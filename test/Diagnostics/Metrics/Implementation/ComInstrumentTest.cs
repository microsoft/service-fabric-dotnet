// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

public abstract class ComInstrumentTest
{
    public sealed class Constructor : ComInstrumentTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenFabricMeterIsNull()
        {
            IFabricMeter fabricMeter = null;
            var exception = Assert.Throws<ArgumentNullException>(() => new ComInstrument(fabricMeter));
            Assert.Equal(nameof(fabricMeter), exception.ParamName);
        }
    }
}
