// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.ServiceFabric.Client.Http;

partial class RepairManagementClient
{
    readonly string externalRepairManagerQuery;

    RepairManagementClient(ServiceFabricHttpClient client, string serviceName) : this(client)
    {
        if (serviceName is null)
            throw new ArgumentNullException(nameof(serviceName));
        if (!Regex.IsMatch(serviceName, @"\Afabric:/System/RepairManagerService/[A-Za-z0-9]+\z",
            RegexOptions.CultureInvariant))
            throw new ArgumentException(
                "Expected fabric:/System/RepairManagerService/ followed by one nonempty ASCII alphanumeric name.",
                nameof(serviceName));

        externalRepairManagerQuery = "ExternalRepairManagerServiceUri=" + Uri.EscapeDataString(serviceName);
    }

    internal static IRepairManagementClient ForService(IServiceFabricClient client, string serviceName)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));
        if (client is not ServiceFabricHttpClient http)
            throw new ArgumentException("Named repair routing requires a ServiceFabricHttpClient.", nameof(client));

        return new RepairManagementClient(http, serviceName);
    }

    void AddExternalRepairManagerQuery(List<string> query)
    {
        if (externalRepairManagerQuery != null)
            query.Add(externalRepairManagerQuery);
    }
}
