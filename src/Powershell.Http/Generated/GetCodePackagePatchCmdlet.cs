// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Powershell.Http
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using Microsoft.ServiceFabric.Common;

    /// <summary>
    /// Gets node-local code package patch mappings and process observations.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "SFCodePackagePatch", DefaultParameterSetName = "GetCodePackagePatchInfoList")]
    public partial class GetCodePackagePatchCmdlet : CommonCmdletBase
    {
        /// <summary>
        /// Gets or sets PatchId. The opaque stable target identifier returned by Create or Get. Pass it unchanged to the SDK
        /// or PowerShell. Raw REST callers must URL-encode the complete value as one path segment. The identifier remains
        /// usable when the original service no longer exists.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "GetCodePackagePatchInfo")]
        public string PatchId { get; set; }

        /// <summary>
        /// Gets or sets PatchIdFilter. Optional stable target identifier returned by Create or Get. Restricts results to that
        /// version-qualified target without requiring a service or package lookup.
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "GetCodePackagePatchInfoList")]
        public string PatchIdFilter { get; set; }

        /// <summary>
        /// Gets or sets ServiceName. The name of a service.
        /// </summary>
        [Parameter(Mandatory = false, Position = 2, ParameterSetName = "GetCodePackagePatchInfoList")]
        [Parameter(Mandatory = false, Position = 2, ParameterSetName = "GetCodePackagePatchInfo")]
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets CodePackageName. The name of code package specified in service manifest registered as part of an
        /// application type in a Service Fabric cluster.
        /// </summary>
        [Parameter(Mandatory = false, Position = 3, ParameterSetName = "GetCodePackagePatchInfoList")]
        [Parameter(Mandatory = false, Position = 3, ParameterSetName = "GetCodePackagePatchInfo")]
        public string CodePackageName { get; set; }

        /// <summary>
        /// Gets or sets CodePackageVersion. Optional declared service-manifest code package version. Can be used independently
        /// or combined with the other exact-match filters.
        /// </summary>
        [Parameter(Mandatory = false, Position = 4, ParameterSetName = "GetCodePackagePatchInfoList")]
        [Parameter(Mandatory = false, Position = 4, ParameterSetName = "GetCodePackagePatchInfo")]
        public string CodePackageVersion { get; set; }

        /// <summary>
        /// Gets or sets PartitionId. The identity of the partition.
        /// </summary>
        [Parameter(Mandatory = false, Position = 5, ParameterSetName = "GetCodePackagePatchInfoList")]
        [Parameter(Mandatory = false, Position = 5, ParameterSetName = "GetCodePackagePatchInfo")]
        public PartitionId PartitionId { get; set; }

        /// <summary>
        /// Gets or sets Nodes. Optional comma-separated node names in one query parameter, for example N1,N2. Use CSV double
        /// quotes around a name containing a comma or double quote, doubling an embedded double quote. Supply the raw CSV
        /// value to the SDK or PowerShell. Raw REST callers must URL-encode the complete CSV value.
        /// Omission targets all currently-up nodes once for this call. Empty entries are invalid and duplicate names are
        /// removed. Do not repeat the Nodes parameter; the gateway retains only the first occurrence.
        /// </summary>
        [Parameter(Mandatory = false, Position = 6, ParameterSetName = "GetCodePackagePatchInfoList")]
        [Parameter(Mandatory = false, Position = 6, ParameterSetName = "GetCodePackagePatchInfo")]
        public string Nodes { get; set; }

        /// <summary>
        /// Gets or sets ServerTimeout. The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.
        /// </summary>
        [Parameter(Mandatory = false, Position = 7, ParameterSetName = "GetCodePackagePatchInfoList")]
        [Parameter(Mandatory = false, Position = 7, ParameterSetName = "GetCodePackagePatchInfo")]
        public long? ServerTimeout { get; set; }

        /// <inheritdoc/>
        protected override void ProcessRecordInternal()
        {
            if (this.ParameterSetName.Equals("GetCodePackagePatchInfoList"))
            {
                var result = this.ServiceFabricClient.CodePackages.GetCodePackagePatchInfoListAsync(
                    patchIdFilter: this.PatchIdFilter,
                    serviceName: this.ServiceName,
                    codePackageName: this.CodePackageName,
                    codePackageVersion: this.CodePackageVersion,
                    partitionId: this.PartitionId,
                    nodes: this.Nodes,
                    serverTimeout: this.ServerTimeout,
                    cancellationToken: this.CancellationToken).GetAwaiter().GetResult();

                if (result != null)
                {
                    this.WriteObject(this.FormatOutput(result));
                }
            }
            else if (this.ParameterSetName.Equals("GetCodePackagePatchInfo"))
            {
                var result = this.ServiceFabricClient.CodePackages.GetCodePackagePatchInfoAsync(
                    patchId: this.PatchId,
                    serviceName: this.ServiceName,
                    codePackageName: this.CodePackageName,
                    codePackageVersion: this.CodePackageVersion,
                    partitionId: this.PartitionId,
                    nodes: this.Nodes,
                    serverTimeout: this.ServerTimeout,
                    cancellationToken: this.CancellationToken).GetAwaiter().GetResult();

                if (result != null)
                {
                    this.WriteObject(this.FormatOutput(result));
                }
            }
        }

        /// <inheritdoc/>
        protected override object FormatOutput(object output)
        {
            return output;
        }
    }
}
