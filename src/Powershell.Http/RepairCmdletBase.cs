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
    /// Gets or sets the configured service URI of an External Repair Manager instance.
    /// Omit this parameter to use the original Repair Manager.
    /// </summary>
    [Parameter(DontShow = true), ValidateNotNullOrEmpty()]
    public string ExternalRepairManagerServiceName { get; set; }

    /// <summary>
    /// Gets the client for this invocation without changing the cluster connection's default repair client.
    /// </summary>
    protected IRepairManagementClient RepairClient =>
        MyInvocation.BoundParameters.ContainsKey(nameof(ExternalRepairManagerServiceName))
            ? RepairManagementClient.ForService(ServiceFabricClient, ExternalRepairManagerServiceName)
            : ServiceFabricClient.Repairs;
}
