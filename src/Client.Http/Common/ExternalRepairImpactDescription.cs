// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;

namespace Microsoft.ServiceFabric.Common;

sealed class ExternalRepairImpactDescription : RepairImpactDescriptionBase
{
    internal readonly ExternalRepairImpactLevel ImpactLevel;

    internal ExternalRepairImpactDescription(string impactLevel) : base(RepairImpactKind.External)
    {
        if (impactLevel is null)
            throw new ArgumentNullException(nameof(impactLevel));
        if (!Enum.TryParse(impactLevel, true, out ImpactLevel) ||
            !Enum.IsDefined(typeof(ExternalRepairImpactLevel), ImpactLevel))
            throw new ArgumentException("Unknown external repair impact level.", nameof(impactLevel));
    }
}
