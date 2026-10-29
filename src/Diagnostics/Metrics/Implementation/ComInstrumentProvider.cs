// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Fabric.Interop;
using System.Runtime.InteropServices;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Creates instruments with a native FabricTelemetry.dll meter provider. The native code validates meter names, applies the
/// Telemetry/Metrics configuration and exports the recorded values, including when the provider is released.
/// </summary>
sealed class ComInstrumentProvider : IInstrumentProvider
{
    readonly IReadOnlyCollection<string> fixedDimensionNames;
    readonly IReadOnlyCollection<string> fixedDimensionValues;
    IFabricMeterProvider2 fabricMeterProvider;

    static Func<IFabricMeterProvider2> createFabricMeterProvider = NativeTelemetry.FabricCreateMeterProvider;
    static Func<object, int> finalReleaseComObject = Utility.FinalReleaseComObject;

    internal ComInstrumentProvider(IReadOnlyCollection<string> fixedDimensionNames, IReadOnlyCollection<string> fixedDimensionValues)
    {
        fabricMeterProvider = createFabricMeterProvider();
        this.fixedDimensionNames = fixedDimensionNames;
        this.fixedDimensionValues = fixedDimensionValues;
    }

    IInstrument IInstrumentProvider.CreateInstrument(string metricNamespace, string name, string[] variableDimensionNames) =>
        new ComInstrument(CreateNativeMeter(metricNamespace, name, variableDimensionNames));

    void IDisposable.Dispose()
    {
        if (!IsDisposed())
        {
            finalReleaseComObject(fabricMeterProvider);
            fabricMeterProvider = null;
        }
    }

    bool IsDisposed() => fabricMeterProvider == null;

    unsafe IFabricMeter CreateNativeMeter(string metricNamespace, string metricName, IEnumerable<string> variableDimensionNames)
    {
        if (IsDisposed())
            throw new ObjectDisposedException(nameof(MeterProvider<>));

        string[] allDimensionNames = [.. fixedDimensionNames, .. variableDimensionNames];
        string[] fixedDimValues = [.. fixedDimensionValues];
        int totalPins = 2 + allDimensionNames.Length + fixedDimValues.Length;

        GCHandle* pins = stackalloc GCHandle[totalPins];
        IntPtr* dimensionNamePtrs = stackalloc IntPtr[allDimensionNames.Length];
        IntPtr* fixedValuePtrs = stackalloc IntPtr[fixedDimValues.Length];

        try
        {
            int p = 0;
            pins[p++] = GCHandle.Alloc(metricNamespace, GCHandleType.Pinned);
            pins[p++] = GCHandle.Alloc(metricName, GCHandleType.Pinned);

            for (int i = 0; i < allDimensionNames.Length; i++)
                pins[p++] = GCHandle.Alloc(allDimensionNames[i], GCHandleType.Pinned);

            for (int i = 0; i < fixedDimValues.Length; i++)
                pins[p++] = GCHandle.Alloc(fixedDimValues[i], GCHandleType.Pinned);

            for (int i = 0; i < allDimensionNames.Length; i++)
                dimensionNamePtrs[i] = pins[2 + i].AddrOfPinnedObject();

            for (int i = 0; i < fixedDimValues.Length; i++)
                fixedValuePtrs[i] = pins[2 + allDimensionNames.Length + i].AddrOfPinnedObject();

            FABRIC_METER_DESCRIPTION description;
            description.Namespace = pins[0].AddrOfPinnedObject();
            description.Name = pins[1].AddrOfPinnedObject();
            description.TotalDimensionsCount = (uint)allDimensionNames.Length;
            description.DimensionNames = (IntPtr)dimensionNamePtrs;
            description.FixedDimensionCount = (uint)fixedDimValues.Length;
            description.FixedDimensionValues = (IntPtr)fixedValuePtrs;
            description.Reserved = IntPtr.Zero;

            return fabricMeterProvider.CreateMeter2((IntPtr)(&description));
        }
        finally
        {
            Interop.Free(pins, totalPins);
        }
    }
}
