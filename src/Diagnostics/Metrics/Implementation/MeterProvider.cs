// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Fabric;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation
{
    abstract class MeterProvider<TValueType> : IMeterProvider<TValueType>
    {
        readonly IInstrumentProvider instrumentProvider;

        protected MeterProvider(ServiceContext serviceContext = null)
        {
            IReadOnlyCollection<string> fixedDimensionNames;
            IReadOnlyCollection<string> fixedDimensionValues;

            if (serviceContext != null)
            {
                fixedDimensionNames =
                [
                    nameof(ServiceContext.PartitionId),
                    nameof(ServiceContext.ServiceTypeName),
                    nameof(ServiceContext.ServiceName),
                    nameof(ServiceContext.CodePackageActivationContext.ApplicationName),
                    nameof(ServiceContext.CodePackageActivationContext.ApplicationTypeName)
                ];

                fixedDimensionValues =
                [
                    serviceContext.PartitionId.ToString(),
                    serviceContext.ServiceTypeName,
                    serviceContext.ServiceName.ToString(),
                    serviceContext.CodePackageActivationContext.ApplicationName,
                    serviceContext.CodePackageActivationContext.ApplicationTypeName
                ];
            }
            else
            {
                fixedDimensionNames = [];
                fixedDimensionValues = [];
            }

            instrumentProvider = new ComInstrumentProvider(fixedDimensionNames, fixedDimensionValues);
        }

        protected IInstrument CreateInstrument(string metricNamespace, string metricName, string[] variableDimensionNames) =>
            instrumentProvider.CreateInstrument(metricNamespace, metricName, variableDimensionNames);

        public void Dispose() => instrumentProvider.Dispose();

        public abstract IMeter<TValueType> CreateMeter(string metricNamespace, string name);
        public abstract IMeter1D<TValueType> CreateMeter(string metricNamespace, string name, string dimension1Name);
        public abstract IMeter2D<TValueType> CreateMeter(string metricNamespace, string name, string dimension1Name, string dimension2Name);
        public abstract IMeter3D<TValueType> CreateMeter(string metricNamespace, string name, string dimension1Name, string dimension2Name, string dimension3Name);
    }
}
