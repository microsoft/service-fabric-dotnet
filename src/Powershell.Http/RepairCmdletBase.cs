// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System.ComponentModel;
using System.Management.Automation;
using Microsoft.ServiceFabric.Client;
using Microsoft.ServiceFabric.Client.Http;

namespace Microsoft.ServiceFabric.Powershell.Http;

/// <summary>
/// Provides repair-specific instance selection for internal platform operations.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class RepairCmdletBase : CommonCmdletBase
{
    /// <summary>
    /// Gets or sets the ServiceId of the Repair Manager service to use: the service name without the 'fabric:' URI
    /// scheme, with '~' in place of '/', for example System~RepairManagerService~public-a for a named External Repair
    /// Manager. Omit this parameter to use the Default Repair Manager.
    /// </summary>
    [Parameter(DontShow = true), ValidateNotNullOrEmpty()]
    public string ServiceId { get; set; }

    /// <summary>
    /// Gets the client for this invocation without changing the cluster connection's default repair client.
    /// </summary>
    protected IRepairManagementClient RepairClient =>
        MyInvocation.BoundParameters.ContainsKey(nameof(ServiceId))
            ? RepairManagementClient.ForService(ServiceFabricClient, ServiceId)
            : ServiceFabricClient.Repairs;
}
