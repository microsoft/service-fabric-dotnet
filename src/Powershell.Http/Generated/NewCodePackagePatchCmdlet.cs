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
    /// Creates or replaces a code package patch mapping on selected nodes.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "SFCodePackagePatch")]
    public partial class NewCodePackagePatchCmdlet : CommonCmdletBase
    {
        /// <summary>
        /// Gets or sets ImageStore flag
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = "_ImageStore_")]
        public SwitchParameter ImageStore { get; set; }

        /// <summary>
        /// Gets or sets ProvisionedCodePackage flag
        /// </summary>
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = "_ProvisionedCodePackage_")]
        public SwitchParameter ProvisionedCodePackage { get; set; }

        /// <summary>
        /// Gets or sets ServiceName. The service used to identify the application instance and deployed code package.
        /// Package-wide scope can affect other services using that package.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public ServiceName ServiceName { get; set; }

        /// <summary>
        /// Gets or sets CodePackageName. The name of the code package defined in the service manifest.
        /// </summary>
        [Parameter(Mandatory = true, Position = 2)]
        public string CodePackageName { get; set; }

        /// <summary>
        /// Gets or sets CodePackageVersion. The declared service-manifest version of the original code package to patch, not
        /// the replacement source version or internal rollout version.
        /// </summary>
        [Parameter(Mandatory = true, Position = 3)]
        public string CodePackageVersion { get; set; }

        /// <summary>
        /// Gets or sets ImageStoreRelativePath. The Image Store-relative directory containing the complete replacement code
        /// package.
        /// </summary>
        [Parameter(Mandatory = true, Position = 4, ParameterSetName = "_ImageStore_")]
        public string ImageStoreRelativePath { get; set; }

        /// <summary>
        /// Gets or sets ApplicationTypeName. The application type name as defined in the application manifest.
        /// </summary>
        [Parameter(Mandatory = true, Position = 5, ParameterSetName = "_ProvisionedCodePackage_")]
        public string ApplicationTypeName { get; set; }

        /// <summary>
        /// Gets or sets ApplicationTypeVersion. The registered application-type version containing the replacement source, not
        /// the original CodePackageVersion being patched.
        /// </summary>
        [Parameter(Mandatory = true, Position = 6, ParameterSetName = "_ProvisionedCodePackage_")]
        public string ApplicationTypeVersion { get; set; }

        /// <summary>
        /// Gets or sets ServiceManifestName. The name of the service manifest.
        /// </summary>
        [Parameter(Mandatory = true, Position = 7, ParameterSetName = "_ProvisionedCodePackage_")]
        public string ServiceManifestName { get; set; }

        /// <summary>
        /// Gets or sets SourceCodePackageName. The name of the code package defined in the service manifest.
        /// </summary>
        [Parameter(Mandatory = true, Position = 8, ParameterSetName = "_ProvisionedCodePackage_")]
        public string SourceCodePackageName { get; set; }

        /// <summary>
        /// Gets or sets PartitionId. Optional partition scope. Omit for package-wide scope. The partition must belong to
        /// ServiceName and use exclusive-process activation.
        /// </summary>
        [Parameter(Mandatory = false, Position = 9)]
        public PartitionId PartitionId { get; set; }

        /// <summary>
        /// Gets or sets ContentName. An optional safe name prefix for a new publication's ContentId. Service Fabric appends a
        /// generated unique identifier. Omit for a generated identifier alone. Create does not apply this prefix or rename
        /// content already in the target application's retained HotPatches layout, whether newly uploaded or previously
        /// published.
        /// </summary>
        [Parameter(Mandatory = false, Position = 10)]
        public string ContentName { get; set; }

        /// <summary>
        /// Gets or sets Nodes. Nodes to target. Omit this property to resolve all currently-up nodes once for this call. An
        /// explicitly empty list is invalid. Duplicate names are removed.
        /// Use an explicit list when retrying so the recipient set does not change between attempts.
        /// </summary>
        [Parameter(Mandatory = false, Position = 11)]
        public IEnumerable<string> Nodes { get; set; }

        /// <summary>
        /// Gets or sets ServerTimeout. The submission budget in seconds, including resolution, content publication, node
        /// acceptance, and replies. Defaults to 1800 seconds (30 minutes). Expiry does not cancel accepted background
        /// downloads or process work.
        /// </summary>
        [Parameter(Mandatory = false, Position = 12)]
        public long? ServerTimeout { get; set; }

        /// <inheritdoc/>
        protected override void ProcessRecordInternal()
        {
            CodePackagePatchSource codePackagePatchSource = null;
            if (this.ImageStore.IsPresent)
            {
                codePackagePatchSource = new ImageStoreCodePackagePatchSource(
                    imageStoreRelativePath: this.ImageStoreRelativePath);
            }
            else if (this.ProvisionedCodePackage.IsPresent)
            {
                codePackagePatchSource = new ProvisionedCodePackagePatchSource(
                    applicationTypeName: this.ApplicationTypeName,
                    applicationTypeVersion: this.ApplicationTypeVersion,
                    serviceManifestName: this.ServiceManifestName,
                    sourceCodePackageName: this.SourceCodePackageName);
            }

            var codePackagePatchCreateDescription = new CodePackagePatchCreateDescription(
            serviceName: this.ServiceName,
            codePackageName: this.CodePackageName,
            codePackageVersion: this.CodePackageVersion,
            source: codePackagePatchSource,
            partitionId: this.PartitionId,
            contentName: this.ContentName,
            nodes: this.Nodes);

            var result = this.ServiceFabricClient.CodePackages.CreateCodePackagePatchAsync(
                codePackagePatchCreateDescription: codePackagePatchCreateDescription,
                serverTimeout: this.ServerTimeout,
                cancellationToken: this.CancellationToken).GetAwaiter().GetResult();

            if (result != null)
            {
                this.WriteObject(this.FormatOutput(result));
            }
        }

        /// <inheritdoc/>
        protected override object FormatOutput(object output)
        {
            return output;
        }
    }
}
