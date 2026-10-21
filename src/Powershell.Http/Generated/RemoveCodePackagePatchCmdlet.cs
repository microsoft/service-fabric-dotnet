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
    /// Deletes a code package patch mapping on selected nodes.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "SFCodePackagePatch")]
    public partial class RemoveCodePackagePatchCmdlet : CommonCmdletBase
    {
        /// <summary>
        /// Gets or sets PatchId. The opaque stable target identifier returned by Create or Get. Pass it unchanged to the SDK
        /// or PowerShell. Raw REST callers must URL-encode the complete value as one path segment. The identifier remains
        /// usable when the original service no longer exists.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0)]
        public string PatchId { get; set; }

        /// <summary>
        /// Gets or sets Nodes. Nodes to target. Omission resolves all currently-up nodes once, which may differ from the
        /// original Create recipients.
        /// An explicitly empty list is invalid. Duplicate names are removed. Delete must eventually reach every accepting or
        /// possibly accepting node.
        /// </summary>
        [Parameter(Mandatory = false, Position = 1)]
        public IEnumerable<string> Nodes { get; set; }

        /// <summary>
        /// Gets or sets ServerTimeout. The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.
        /// </summary>
        [Parameter(Mandatory = false, Position = 2)]
        public long? ServerTimeout { get; set; }

        /// <summary>
        /// Gets or sets the force flag. If provided, then the destructive action will be performed without asking for
        /// confirmation prompt.
        /// </summary>
        [Parameter(Mandatory = false, Position = 3)]
        public SwitchParameter Force { get; set; }

        /// <inheritdoc/>
        protected override void ProcessRecordInternal()
        {
            var codePackagePatchDeleteDescription = new CodePackagePatchDeleteDescription(
            nodes: this.Nodes);

            if (((this.Force != null) && this.Force) || this.ShouldContinue(string.Empty, string.Empty))
            {
                var result = this.ServiceFabricClient.CodePackages.DeleteCodePackagePatchAsync(
                    patchId: this.PatchId,
                    codePackagePatchDeleteDescription: codePackagePatchDeleteDescription,
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
