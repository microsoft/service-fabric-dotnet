// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.ServiceFabric.Client.Http;

partial class RepairManagementClient
{
    // The ServiceId of the Repair Manager service this client targets, or null for the Default Repair Manager.
    readonly string serviceId;

    RepairManagementClient(ServiceFabricHttpClient client, string serviceId) : this(client)
    {
        if (serviceId is null)
            throw new ArgumentNullException(nameof(serviceId));
        if (!Regex.IsMatch(serviceId, @"\ASystem~RepairManagerService(~[A-Za-z0-9][A-Za-z0-9-]*)?\z",
            RegexOptions.CultureInvariant))
            throw new ArgumentException(
                "Expected System~RepairManagerService, optionally followed by ~ and a name of ASCII letters, digits " +
                "and hyphens that does not start with a hyphen.",
                nameof(serviceId));

        this.serviceId = serviceId;
    }

    internal static IRepairManagementClient ForService(IServiceFabricClient client, string serviceId)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));
        if (client is not ServiceFabricHttpClient http)
            throw new ArgumentException("Named repair routing requires a ServiceFabricHttpClient.", nameof(client));

        return new RepairManagementClient(http, serviceId);
    }

    // The gateway honors ServiceId only at api-version 12.0 or later, and released 11.x and older gateways reject
    // api-version 12.0, so a targeted request fails there instead of reaching the Default Repair Manager.
    // Requests without ServiceId keep api-version 6.0.
    void AddApiVersionAndServiceId(List<string> queryParams)
    {
        if (serviceId is null)
        {
            queryParams.Add("api-version=6.0");
            return;
        }

        queryParams.Add("api-version=12.0");
        queryParams.Add("ServiceId=" + serviceId);
    }
}
