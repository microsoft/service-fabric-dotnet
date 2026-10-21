// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceFabric.Client.Exceptions;
    using Microsoft.ServiceFabric.Common;
    using Microsoft.ServiceFabric.Common.Exceptions;

    /// <summary>
    /// Interface containing methods for performing CodePackageClient operations.
    /// </summary>
    public partial interface ICodePackageClient
    {
        /// <summary>
        /// Gets the list of code packages deployed on a Service Fabric node.
        /// </summary>
        /// <remarks>
        /// Gets the list of code packages deployed on a Service Fabric node for the given application.
        /// </remarks>
        /// <param name ="nodeName">The name of the node.</param>
        /// <param name ="applicationId">The identity of the application. This is typically the full name of the application
        /// without the 'fabric:' URI scheme.
        /// Starting from version 6.0, hierarchical names are delimited with the "~" character.
        /// For example, if the application name is "fabric:/myapp/app1", the application identity would be "myapp~app1" in
        /// 6.0+ and "myapp/app1" in previous versions.
        /// </param>
        /// <param name ="serviceManifestName">The name of a service manifest registered as part of an application type in a
        /// Service Fabric cluster.</param>
        /// <param name ="codePackageName">The name of code package specified in service manifest registered as part of an
        /// application type in a Service Fabric cluster.</param>
        /// <param name ="serverTimeout">The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.</param>
        /// <param name ="cancellationToken">Cancels the client-side operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="InvalidCredentialsException">Thrown when invalid credentials are used while making request to cluster.</exception>
        /// <exception cref="ServiceFabricRequestException">Thrown when request to Service Fabric cluster failed due to an underlying issue such as network connectivity, DNS failure or timeout.</exception>
        /// <exception cref="ServiceFabricException">Thrown when the requested operation failed at server. Exception contains Error code <see cref="FabricError.ErrorCode"/>, message indicating the failure. It also contains a flag wether the exception is transient or not, client operations can be retried if its transient.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested for the cancellation token.</exception>
        Task<IEnumerable<DeployedCodePackageInfo>> GetDeployedCodePackageInfoListAsync(
            NodeName nodeName,
            string applicationId,
            string serviceManifestName = default(string),
            string codePackageName = default(string),
            long? serverTimeout = 60,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Restarts a code package deployed on a Service Fabric node in a cluster.
        /// </summary>
        /// <remarks>
        /// Restarts a code package deployed on a Service Fabric node in a cluster. This aborts the code package process, which
        /// will restart all the user service replicas hosted in that process.
        /// </remarks>
        /// <param name ="nodeName">The name of the node.</param>
        /// <param name ="applicationId">The identity of the application. This is typically the full name of the application
        /// without the 'fabric:' URI scheme.
        /// Starting from version 6.0, hierarchical names are delimited with the "~" character.
        /// For example, if the application name is "fabric:/myapp/app1", the application identity would be "myapp~app1" in
        /// 6.0+ and "myapp/app1" in previous versions.
        /// </param>
        /// <param name ="restartDeployedCodePackageDescription">Describes the deployed code package on Service Fabric node to
        /// restart.</param>
        /// <param name ="serverTimeout">The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.</param>
        /// <param name ="cancellationToken">Cancels the client-side operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="InvalidCredentialsException">Thrown when invalid credentials are used while making request to cluster.</exception>
        /// <exception cref="ServiceFabricRequestException">Thrown when request to Service Fabric cluster failed due to an underlying issue such as network connectivity, DNS failure or timeout.</exception>
        /// <exception cref="ServiceFabricException">Thrown when the requested operation failed at server. Exception contains Error code <see cref="FabricError.ErrorCode"/>, message indicating the failure. It also contains a flag wether the exception is transient or not, client operations can be retried if its transient.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested for the cancellation token.</exception>
        Task RestartDeployedCodePackageAsync(
            NodeName nodeName,
            string applicationId,
            RestartDeployedCodePackageDescription restartDeployedCodePackageDescription,
            long? serverTimeout = 60,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets the container logs for container deployed on a Service Fabric node.
        /// </summary>
        /// <remarks>
        /// Gets the container logs for container deployed on a Service Fabric node for the given code package.
        /// </remarks>
        /// <param name ="nodeName">The name of the node.</param>
        /// <param name ="applicationId">The identity of the application. This is typically the full name of the application
        /// without the 'fabric:' URI scheme.
        /// Starting from version 6.0, hierarchical names are delimited with the "~" character.
        /// For example, if the application name is "fabric:/myapp/app1", the application identity would be "myapp~app1" in
        /// 6.0+ and "myapp/app1" in previous versions.
        /// </param>
        /// <param name ="serviceManifestName">The name of a service manifest registered as part of an application type in a
        /// Service Fabric cluster.</param>
        /// <param name ="codePackageName">The name of code package specified in service manifest registered as part of an
        /// application type in a Service Fabric cluster.</param>
        /// <param name ="tail">Number of lines to show from the end of the logs. Default is 100. 'all' to show the complete
        /// logs.</param>
        /// <param name ="previous">Specifies whether to get container logs from exited/dead containers of the code package
        /// instance.</param>
        /// <param name ="serverTimeout">The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.</param>
        /// <param name ="cancellationToken">Cancels the client-side operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="InvalidCredentialsException">Thrown when invalid credentials are used while making request to cluster.</exception>
        /// <exception cref="ServiceFabricRequestException">Thrown when request to Service Fabric cluster failed due to an underlying issue such as network connectivity, DNS failure or timeout.</exception>
        /// <exception cref="ServiceFabricException">Thrown when the requested operation failed at server. Exception contains Error code <see cref="FabricError.ErrorCode"/>, message indicating the failure. It also contains a flag wether the exception is transient or not, client operations can be retried if its transient.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested for the cancellation token.</exception>
        Task<ContainerLogs> GetContainerLogsDeployedOnNodeAsync(
            NodeName nodeName,
            string applicationId,
            string serviceManifestName,
            string codePackageName,
            string tail = default(string),
            bool? previous = false,
            long? serverTimeout = 60,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Invoke container API on a container deployed on a Service Fabric node.
        /// </summary>
        /// <remarks>
        /// Invoke container API on a container deployed on a Service Fabric node for the given code package.
        /// </remarks>
        /// <param name ="nodeName">The name of the node.</param>
        /// <param name ="applicationId">The identity of the application. This is typically the full name of the application
        /// without the 'fabric:' URI scheme.
        /// Starting from version 6.0, hierarchical names are delimited with the "~" character.
        /// For example, if the application name is "fabric:/myapp/app1", the application identity would be "myapp~app1" in
        /// 6.0+ and "myapp/app1" in previous versions.
        /// </param>
        /// <param name ="serviceManifestName">The name of a service manifest registered as part of an application type in a
        /// Service Fabric cluster.</param>
        /// <param name ="codePackageName">The name of code package specified in service manifest registered as part of an
        /// application type in a Service Fabric cluster.</param>
        /// <param name ="codePackageInstanceId">ID that uniquely identifies a code package instance deployed on a service
        /// fabric node.</param>
        /// <param name ="containerApiRequestBody">Parameters for making container API call</param>
        /// <param name ="serverTimeout">The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.</param>
        /// <param name ="cancellationToken">Cancels the client-side operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="InvalidCredentialsException">Thrown when invalid credentials are used while making request to cluster.</exception>
        /// <exception cref="ServiceFabricRequestException">Thrown when request to Service Fabric cluster failed due to an underlying issue such as network connectivity, DNS failure or timeout.</exception>
        /// <exception cref="ServiceFabricException">Thrown when the requested operation failed at server. Exception contains Error code <see cref="FabricError.ErrorCode"/>, message indicating the failure. It also contains a flag wether the exception is transient or not, client operations can be retried if its transient.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested for the cancellation token.</exception>
        Task<ContainerApiResponse> InvokeContainerApiAsync(
            NodeName nodeName,
            string applicationId,
            string serviceManifestName,
            string codePackageName,
            string codePackageInstanceId,
            ContainerApiRequestBody containerApiRequestBody,
            long? serverTimeout = 60,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates or replaces a code package patch mapping on selected nodes.
        /// </summary>
        /// <remarks>
        /// Publishes or reuses one complete replacement code package under Store/{ApplicationType}/HotPatches/{ContentId},
        /// then submits a mapping to each selected node.
        /// Requires Admin access and EnableHotPatching to be enabled. Package scope supports a single executable code package
        /// per shared or exclusive host. Partition scope requires exclusive activation.
        /// Only customer executable packages with package-relative entry points are supported. Containers, DllHost, service
        /// groups, system applications, and multiple code packages per host are not supported. Setup and main retain their
        /// existing execution identities and use the same selected package directory.
        /// Application and package versions, manifests, configuration, and data locations remain unchanged. Deployed package
        /// files are not overwritten.
        /// ServiceName, CodePackageName, and CodePackageVersion identify package-wide scope for that declared code package
        /// version. Providing PartitionId narrows the scope to that partition. Before finalizing publication, Service Fabric
        /// validates that the application instance, registered application type, service type, service, code package name and
        /// version, and optional partition exist and belong together.
        /// Before node submission, Service Fabric finalizes the source using the normal Image Store checksum and validation
        /// policy. Upload a local directory using existing Image Store operations before submitting this request. The JSON
        /// request references stored or provisioned contents, not a caller-local path or inline bytes. Content already
        /// uploaded to Store/{ApplicationType}/HotPatches/{ContentId} for the target application type is validated and
        /// finalized in place without another copy or ContentId, and ContentName is not applied again. Full Create preflight
        /// precedes finalization, not the earlier upload; a rejected Create does not remove completed uploaded content.
        /// Incomplete publication submits no node requests.
        /// Service Fabric prepares the immutable version-qualified target, replacement checksum, and per-node revision checks
        /// internally. Each node validates that prepared request, then durably saves acceptance before downloading or
        /// restarting processes. SuccessfulNodes means accepted intent, not downloaded content, running code, or application
        /// health.
        /// PatchId is a stable, decodable identifier of the resolved application instance, service package, code package name
        /// and declared version, and scope. Repeated Create calls to the same target return the same PatchId. Mappings for
        /// different code package versions are independent and can coexist on a node.
        /// ContentId identifies one immutable publication independently of PatchId. A new publication uses an optional
        /// ContentName prefix followed by a generated unique identifier. Newly uploaded content already in the target type's
        /// retained layout is finalized in place. A completed retained publication owned by the same application type reuses
        /// its ContentId and path without another upload or copy; other sources are copied under the target type. Publication
        /// retries retain ContentId. Independently uploaded identical bytes do not imply the same publication, and a new
        /// publication must not overwrite or merge with another publication.
        /// After publication succeeds, the response includes PatchId, ContentId, and the complete retained
        /// ImageStoreRelativePath even if every node rejects the request or has an unresolved outcome. Automatic remote
        /// cleanup occurs only when the last registered version of the owning application type is unregistered. Delete and
        /// application-instance deletion do not delete published content. Remote deletion does not revoke cached mappings.
        /// The response contains independent outcomes for the resolved node set. The HTTP 200 response does not imply that
        /// every node accepted the patch. UnresolvedNodes may still accept delayed requests.
        /// The server timeout covers resolution, publication, submission, and replies, not background work. A transport
        /// failure or missing response leaves acceptance unknown for nodes that may have received the request. Query those
        /// nodes before making a new decision. Internal retries reuse the original prepared target, content, recipient set,
        /// and revisions; they do not silently adopt newer state. Reapplying content that is already selected does not cause
        /// another restart.
        /// Hosting continues accepted work after disconnection or restart with retained node data. Disabling EnableHotPatching
        /// does not cancel accepted work or remove mappings. Nodes without current activations or running a different code
        /// package version can accept and prepare content. They do not restart a different-version process. Get reports
        /// DifferentCodePackageVersion as a non-error observation while the mapping remains Active.
        /// Matching uses the declared code package version, not an original checksum or launch-settings fingerprint. Current
        /// manifest settings, including entry points, arguments, and execution identities, remain effective. Normal
        /// replacement checksum, layout, entry-point, access, revision, and exact-process checks still apply. Missing a
        /// required entry point blocks the mapping rather than falling back to deployed code. Content must be ready before a
        /// patch-triggered stop, and a new activation selecting a pending replacement waits for it.
        /// The caller controls node selection, batching, and primary moves. This operation obtains no FM approval and does not
        /// guarantee quorum or coordinate unrelated maintenance.
        /// </remarks>
        /// <param name ="codePackagePatchCreateDescription">The service, code package name and declared version, optional
        /// partition, one replacement source, optional content name, and recipient nodes. Service Fabric prepares validation
        /// metadata internally.</param>
        /// <param name ="serverTimeout">The submission budget in seconds, including resolution, content publication, node
        /// acceptance, and replies. Defaults to 1800 seconds (30 minutes). Expiry does not cancel accepted background
        /// downloads or process work.</param>
        /// <param name ="cancellationToken">Cancels the client-side operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="InvalidCredentialsException">Thrown when invalid credentials are used while making request to cluster.</exception>
        /// <exception cref="ServiceFabricRequestException">Thrown when request to Service Fabric cluster failed due to an underlying issue such as network connectivity, DNS failure or timeout.</exception>
        /// <exception cref="ServiceFabricException">Thrown when the requested operation failed at server. Exception contains Error code <see cref="FabricError.ErrorCode"/>, message indicating the failure. It also contains a flag wether the exception is transient or not, client operations can be retried if its transient.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested for the cancellation token.</exception>
        Task<CodePackagePatchResult> CreateCodePackagePatchAsync(
            CodePackagePatchCreateDescription codePackagePatchCreateDescription,
            long? serverTimeout = 1800,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Deletes a code package patch mapping on selected nodes.
        /// </summary>
        /// <remarks>
        /// Requires Admin access and remains available when EnableHotPatching is disabled. The operation removes the current
        /// mapping at the version-qualified target identified by PatchId, not its Image Store package or application data.
        /// Service Fabric decodes the target from PatchId and prepares revision checks internally. No current service lookup
        /// or separate service, code package, version, or partition input is required. Each accepting node durably excludes
        /// the selected mapping from launch selection. If there is no previous mapping, it saves a removal revision marker
        /// that rejects an earlier Create prepared against the old revision. Hosting then moves affected processes to another
        /// applicable mapping or the original deployed code. Completion is observed with GetCodePackagePatchInfoList.
        /// Clearing a partition mapping may expose a package-wide mapping for the same code package version. Create A, Create
        /// B, then Delete at the same target does not restore A. Clearing one version does not clear another version's
        /// mapping. Late download or removal completion cannot overwrite newer intent.
        /// The response reports independent outcomes. SuccessfulNodes means accepted removal intent, not that every process
        /// has switched. UnresolvedNodes have unknown acceptance and must be queried and cleared when reachable. There is no
        /// central worker that retains an undelivered Delete for returning nodes.
        /// Use the PatchId returned by Create or Get, including when the original service no longer exists. A fresh Delete
        /// acts on the current mapping, which may select different content from an earlier Create with the same PatchId.
        /// Internal retries retain the original target, recipient set, and revision checks rather than clearing a later
        /// replacement. Ordering protection begins when the revision is captured, not when the outer request is sent. Callers
        /// do not supply per-node revision metadata. A missing response leaves acceptance unknown; query those nodes before
        /// making a new removal decision.
        /// The server timeout covers acceptance and replies, not background process work. Delete does not wait for broken
        /// replacement code to become healthy. Existing access, revision, and exact-process checks still apply, and the caller
        /// must control restart sequencing.
        /// </remarks>
        /// <param name ="patchId">The opaque stable target identifier returned by Create or Get. Pass it unchanged to the SDK
        /// or PowerShell. Raw REST callers must URL-encode the complete value as one path segment. The identifier remains
        /// usable when the original service no longer exists.</param>
        /// <param name ="codePackagePatchDeleteDescription">Optional recipient nodes. Omit the body or Nodes to resolve all
        /// currently-up nodes once for this call. Service Fabric decodes the target from the path identifier and prepares
        /// revision checks internally.</param>
        /// <param name ="serverTimeout">The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.</param>
        /// <param name ="cancellationToken">Cancels the client-side operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="InvalidCredentialsException">Thrown when invalid credentials are used while making request to cluster.</exception>
        /// <exception cref="ServiceFabricRequestException">Thrown when request to Service Fabric cluster failed due to an underlying issue such as network connectivity, DNS failure or timeout.</exception>
        /// <exception cref="ServiceFabricException">Thrown when the requested operation failed at server. Exception contains Error code <see cref="FabricError.ErrorCode"/>, message indicating the failure. It also contains a flag wether the exception is transient or not, client operations can be retried if its transient.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested for the cancellation token.</exception>
        Task<CodePackagePatchResult> DeleteCodePackagePatchAsync(
            string patchId,
            CodePackagePatchDeleteDescription codePackagePatchDeleteDescription = default(CodePackagePatchDeleteDescription),
            long? serverTimeout = 60,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets node-local code package patch mappings and process observations.
        /// </summary>
        /// <remarks>
        /// A read-only query with optional target and node filters in the request URL. There is no request body.
        /// Requires normal query access and remains available when EnableHotPatching is disabled. It does not apply, clear,
        /// cancel, or retry a patch.
        /// Without target filters, lists retained mappings, including dormant, different-version, clearing, and cleared
        /// records. A reachable node with no mappings is reported explicitly, not omitted as if it were unreachable. Use an
        /// unfiltered query to find saved mappings when the original service no longer exists.
        /// PatchId, ServiceName, CodePackageName, CodePackageVersion, and PartitionId are independently optional exact-match
        /// filters combined with AND. PatchId identifies a stable version-qualified target. ServiceName restricts results to
        /// the package scope resolved from that service. CodePackageName can be used alone to find matching package names
        /// across applications. PartitionId restricts results to mappings recorded for that partition; it does not include
        /// package-wide mappings without a partition scope.
        /// Nodes selects which nodes receive the query, rather than hiding failed or unanswered nodes from the response. Each
        /// reachable node returns its matching retained records or an empty Patches list when none match. A missing current
        /// replica is not itself an error. No caller-supplied target or revision preparation is required.
        /// Saved acceptance is separate from content readiness and effective process selection. DifferentCodePackageVersion
        /// explains why an Active mapping is not running; it is not an error. Each process reports its declared code package
        /// version and effective replacement ContentId where present. ReplacementRunning does not establish application
        /// health. A response cannot prove the outcome of an earlier request that has not yet arrived, and missing records or
        /// traces do not prove that a node was never patched.
        /// FailedNodes and UnresolvedNodes carry no inferred mapping or process state. Results describe the selected nodes at
        /// their individual query times, not a cluster-wide atomic snapshot. Use target filters and an explicit node list to
        /// limit the query scope.
        /// </remarks>
        /// <param name ="patchIdFilter">Optional stable target identifier returned by Create or Get. Restricts results to that
        /// version-qualified target without requiring a service or package lookup.</param>
        /// <param name ="serviceName">The name of a service.</param>
        /// <param name ="codePackageName">The name of code package specified in service manifest registered as part of an
        /// application type in a Service Fabric cluster.</param>
        /// <param name ="codePackageVersion">Optional declared service-manifest code package version. Can be used
        /// independently or combined with the other exact-match filters.</param>
        /// <param name ="partitionId">The identity of the partition.</param>
        /// <param name ="nodes">Optional comma-separated node names in one query parameter, for example N1,N2. Use CSV double
        /// quotes around a name containing a comma or double quote, doubling an embedded double quote. Supply the raw CSV
        /// value to the SDK or PowerShell. Raw REST callers must URL-encode the complete CSV value.
        /// Omission targets all currently-up nodes once for this call. Empty entries are invalid and duplicate names are
        /// removed. Do not repeat the Nodes parameter; the gateway retains only the first occurrence.
        /// </param>
        /// <param name ="serverTimeout">The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.</param>
        /// <param name ="cancellationToken">Cancels the client-side operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="InvalidCredentialsException">Thrown when invalid credentials are used while making request to cluster.</exception>
        /// <exception cref="ServiceFabricRequestException">Thrown when request to Service Fabric cluster failed due to an underlying issue such as network connectivity, DNS failure or timeout.</exception>
        /// <exception cref="ServiceFabricException">Thrown when the requested operation failed at server. Exception contains Error code <see cref="FabricError.ErrorCode"/>, message indicating the failure. It also contains a flag wether the exception is transient or not, client operations can be retried if its transient.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested for the cancellation token.</exception>
        Task<CodePackagePatchResult> GetCodePackagePatchInfoListAsync(
            string patchIdFilter = default(string),
            string serviceName = default(string),
            string codePackageName = default(string),
            string codePackageVersion = default(string),
            PartitionId partitionId = default(PartitionId),
            string nodes = default(string),
            long? serverTimeout = 60,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets node-local observations for one code package patch target.
        /// </summary>
        /// <remarks>
        /// A read-only query for the stable version-qualified target identified by patchId, including when its original
        /// service no longer exists. There is no request body.
        /// Requires normal query access and remains available when EnableHotPatching is disabled. It does not create, delete,
        /// cancel, or retry a patch.
        /// ServiceName, CodePackageName, CodePackageVersion, and PartitionId are optional exact-match filters combined with
        /// the path identifier using AND. ServiceName restricts results to the package scope resolved from that service.
        /// PartitionId restricts results to mappings recorded for that partition and does not include package-wide mappings
        /// without a partition scope. Conflicting filters return no matching mappings rather than selecting a different
        /// target.
        /// Nodes selects which nodes receive the query. Each reachable node returns its matching retained mapping or an empty
        /// Patches list when none matches. A missing current replica is not itself an error. No caller-supplied target or
        /// revision preparation is required.
        /// Saved acceptance is separate from content readiness and effective process selection. Nodes with the same PatchId
        /// can select different ContentIds. DifferentCodePackageVersion is a non-error observation, and ReplacementRunning
        /// does not establish application health.
        /// FailedNodes and UnresolvedNodes carry no inferred mapping or process state. A response cannot prove the outcome of
        /// an earlier request that has not yet arrived. Results describe the selected nodes at their individual query times,
        /// not a cluster-wide atomic snapshot.
        /// </remarks>
        /// <param name ="patchId">The opaque stable target identifier returned by Create or Get. Pass it unchanged to the SDK
        /// or PowerShell. Raw REST callers must URL-encode the complete value as one path segment. The identifier remains
        /// usable when the original service no longer exists.</param>
        /// <param name ="serviceName">The name of a service.</param>
        /// <param name ="codePackageName">The name of code package specified in service manifest registered as part of an
        /// application type in a Service Fabric cluster.</param>
        /// <param name ="codePackageVersion">Optional declared service-manifest code package version. Can be used
        /// independently or combined with the other exact-match filters.</param>
        /// <param name ="partitionId">The identity of the partition.</param>
        /// <param name ="nodes">Optional comma-separated node names in one query parameter, for example N1,N2. Use CSV double
        /// quotes around a name containing a comma or double quote, doubling an embedded double quote. Supply the raw CSV
        /// value to the SDK or PowerShell. Raw REST callers must URL-encode the complete CSV value.
        /// Omission targets all currently-up nodes once for this call. Empty entries are invalid and duplicate names are
        /// removed. Do not repeat the Nodes parameter; the gateway retains only the first occurrence.
        /// </param>
        /// <param name ="serverTimeout">The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.</param>
        /// <param name ="cancellationToken">Cancels the client-side operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="InvalidCredentialsException">Thrown when invalid credentials are used while making request to cluster.</exception>
        /// <exception cref="ServiceFabricRequestException">Thrown when request to Service Fabric cluster failed due to an underlying issue such as network connectivity, DNS failure or timeout.</exception>
        /// <exception cref="ServiceFabricException">Thrown when the requested operation failed at server. Exception contains Error code <see cref="FabricError.ErrorCode"/>, message indicating the failure. It also contains a flag wether the exception is transient or not, client operations can be retried if its transient.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested for the cancellation token.</exception>
        Task<CodePackagePatchResult> GetCodePackagePatchInfoAsync(
            string patchId,
            string serviceName = default(string),
            string codePackageName = default(string),
            string codePackageVersion = default(string),
            PartitionId partitionId = default(PartitionId),
            string nodes = default(string),
            long? serverTimeout = 60,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
