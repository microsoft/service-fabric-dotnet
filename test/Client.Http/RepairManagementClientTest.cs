// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Microsoft.ServiceFabric.Common;
using Microsoft.ServiceFabric.Common.Exceptions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.ServiceFabric.Client.Http;

public abstract class RepairManagementClientTest : IDisposable
{
    readonly IRepairManagementClient sut;
    readonly IServiceFabricClient client;
    readonly Recorder recorder = new();
    readonly RepairTask task = new(fuzzy.String(), State.Claimed, fuzzy.String(), version: "123",
        target: new ExternalRepairTargetDescription(), executor: fuzzy.String(),
        impact: new ExternalRepairImpactDescription("RestartNode"));

    const string ServiceName = "fabric:/System/RepairManagerService/ext01";
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    RepairManagementClientTest()
    {
        client = new ServiceFabricClientBuilder()
            .UseEndpoints(new Uri("http://unused.invalid:19080"))
            .ConfigureHttpClientSettings(new HttpClientHandler(), recorder)
            .BuildAsync().GetAwaiter().GetResult();
        sut = RepairManagementClient.ForService(client, ServiceName);
    }

    void IDisposable.Dispose() =>
        client.Dispose();

    public sealed class ForService : RepairManagementClientTest
    {
        [Theory]
        [InlineData("ext")]
        [InlineData("ext01")]
        [InlineData("alpha")]
        [InlineData("RM2")]
        [InlineData("0")]
        [InlineData("01")]
        public async Task AcceptsOneAlphanumericSuffix(string suffix)
        {
            string serviceName = "fabric:/System/RepairManagerService/" + suffix;
            IRepairManagementClient named = RepairManagementClient.ForService(client, serviceName);

            _ = await named.GetRepairTaskListAsync(cancellationToken: TestContext.Current.CancellationToken);

            Request request = Assert.Single(recorder.Requests);
            Assert.Equal("/$/GetRepairTaskList?ExternalRepairManagerServiceUri=" +
                Uri.EscapeDataString(serviceName) + "&api-version=6.0", request.Uri.PathAndQuery);
        }

        [Fact]
        public void RejectsNullServiceName()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => RepairManagementClient.ForService(client, null));
            Assert.Equal("serviceName", exception.ParamName);
            Assert.Empty(recorder.Requests);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("fabric:/System/RepairManagerService")]
        [InlineData("fabric:/System/RepairManagerService/")]
        [InlineData("fabric:/System/RepairManagerService/ext-1")]
        [InlineData("fabric:/System/RepairManagerService/ext_1")]
        [InlineData("fabric:/System/RepairManagerService/ext1/other")]
        [InlineData("fabric:/System/RepairManagerService/ext1\n")]
        [InlineData("fabric:/System/RepairManagerService/\u00e9")]
        [InlineData("fabric:/System/RepairManagerService/ext1?x=y")]
        [InlineData("fabric:/System/RepairManagerService/ext1#fragment")]
        [InlineData("fabric:/System/RepairManagerService/ext1&ExternalRepairManagerServiceUri=other")]
        [InlineData("fabric:/System/RepairManagerService/%65xt1")]
        [InlineData("fabric%3A%2FSystem%2FRepairManagerService%2Fext1")]
        [InlineData("'fabric:/System/RepairManagerService/ext1'")]
        [InlineData("Fabric:/System/RepairManagerService/ext1")]
        [InlineData("fabric:/System/ERM/ext")]
        public void RejectsInvalidServiceNameBeforeSending(string serviceName)
        {
            var exception = Assert.Throws<ArgumentException>(() => RepairManagementClient.ForService(client, serviceName));
            Assert.Equal(nameof(serviceName), exception.ParamName);
            Assert.Empty(recorder.Requests);
        }

        [Fact]
        public void RejectsNullClient()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => RepairManagementClient.ForService(null, ServiceName));
            Assert.Equal(nameof(client), exception.ParamName);
        }

        [Fact]
        public void RejectsUnsupportedClientWithoutFallingBack()
        {
            IServiceFabricClient other = Mock.Of<IServiceFabricClient>();
            var exception = Assert.Throws<ArgumentException>(() => RepairManagementClient.ForService(other, ServiceName));
            Assert.Equal(nameof(client), exception.ParamName);
            Mock.Get(other).VerifyNoOtherCalls();
        }

        [Fact]
        public async Task KeepsConcurrentNamedClientsAndDefaultClientIsolated()
        {
            IRepairManagementClient original = client.Repairs;
            IRepairManagementClient other = RepairManagementClient.ForService(client, "fabric:/System/RepairManagerService/RM2");
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            recorder.Release = release.Task;

            Task pending = Task.WhenAll(
                sut.GetRepairTaskListAsync(cancellationToken: cancellation),
                other.GetRepairTaskListAsync(cancellationToken: cancellation),
                original.GetRepairTaskListAsync(cancellationToken: cancellation));
            int inFlight = recorder.Requests.Count;
            release.SetResult(true);
            await pending;

            Assert.Same(original, client.Repairs);
            Assert.Equal(3, inFlight);
            Assert.Equal(3, recorder.Requests.Count);
            Assert.Contains(recorder.Requests, request => request.Uri.Query == "?api-version=6.0");
            Assert.Contains(recorder.Requests, request => request.Uri.Query ==
                "?ExternalRepairManagerServiceUri=fabric%3A%2FSystem%2FRepairManagerService%2Fext01&api-version=6.0");
            Assert.Contains(recorder.Requests, request => request.Uri.Query ==
                "?ExternalRepairManagerServiceUri=fabric%3A%2FSystem%2FRepairManagerService%2FRM2&api-version=6.0");
        }
    }

    public sealed class Requests : RepairManagementClientTest
    {
        [Theory]
        [InlineData("CreateRepairTask")]
        [InlineData("CancelRepairTask")]
        [InlineData("DeleteRepairTask")]
        [InlineData("GetRepairTaskList")]
        [InlineData("ForceApproveRepairTask")]
        [InlineData("UpdateRepairTaskHealthPolicy")]
        [InlineData("UpdateRepairExecutionState")]
        public async Task ChangesOnlyTheTargetQueryForEveryExistingOperation(string operation)
        {
            await Send(client.Repairs, operation);
            await Send(sut, operation);

            Request[] requests = recorder.Requests.ToArray();
            Assert.Equal(2, requests.Length);
            Assert.Equal("/$/" + operation, requests[0].Uri.AbsolutePath);
            Assert.Equal(requests[0].Uri.AbsolutePath, requests[1].Uri.AbsolutePath);
            Assert.Equal(operation == "GetRepairTaskList" ? HttpMethod.Get : HttpMethod.Post, requests[1].Method);
            Assert.Equal(requests[0].Method, requests[1].Method);
            Assert.Equal(requests[0].Body, requests[1].Body);
            Assert.Equal(operation == "GetRepairTaskList"
                ? "?TaskIdFilter=prefix&StateFilter=2&ExecutorFilter=executor&api-version=6.0"
                : "?api-version=6.0", requests[0].Uri.Query);
            Assert.DoesNotContain("ExternalRepairManagerServiceUri", requests[0].Uri.Query);
            Assert.Equal("?ExternalRepairManagerServiceUri=" + Uri.EscapeDataString(ServiceName) +
                "&" + requests[0].Uri.Query.Substring(1), requests[1].Uri.Query);
            Assert.DoesNotContain("%253A", requests[1].Uri.Query);
            if (operation != "GetRepairTaskList")
            {
                JObject body = JObject.Parse(requests[1].Body);
                Assert.Equal(task.TaskId, (string)body["TaskId"]);
                Assert.Equal("123", (string)body["Version"]);
            }
        }

        [Fact]
        public async Task ReturnsMutationVersion()
        {
            RepairTaskUpdateInfo result = await sut.CreateRepairTaskAsync(task, TestContext.Current.CancellationToken);
            Assert.Equal("456", result.Version);
        }

        [Fact]
        public async Task PreservesServerErrorWithoutRetryingTheDefaultManager()
        {
            recorder.Status = HttpStatusCode.NotFound;
            recorder.Response = "{\"Error\":{\"Code\":\"FABRIC_E_SERVICE_DOES_NOT_EXIST\",\"Message\":\"named instance missing\"}}";

            var exception = await Assert.ThrowsAsync<ServiceFabricException>(() =>
                sut.DeleteRepairTaskAsync(new RepairTaskDeleteDescription(task.TaskId), TestContext.Current.CancellationToken));

            Assert.Contains("named instance missing", exception.Message);
            Assert.Contains("ExternalRepairManagerServiceUri=", Assert.Single(recorder.Requests).Uri.Query);
        }

        [Fact]
        public async Task HonorsCancellationBeforeSending()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sut.GetRepairTaskListAsync(cancellationToken: cancellation.Token));

            Assert.Empty(recorder.Requests);
        }

        Task Send(IRepairManagementClient repairs, string operation)
        {
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            return operation switch
            {
                "CreateRepairTask" => repairs.CreateRepairTaskAsync(task, cancellation),
                "CancelRepairTask" => repairs.CancelRepairTaskAsync(new RepairTaskCancelDescription(task.TaskId, "123", true), cancellation),
                "DeleteRepairTask" => repairs.DeleteRepairTaskAsync(new RepairTaskDeleteDescription(task.TaskId, "123"), cancellation),
                "GetRepairTaskList" => repairs.GetRepairTaskListAsync("prefix", 2, "executor", cancellation),
                "ForceApproveRepairTask" => repairs.ForceApproveRepairTaskAsync(new RepairTaskApproveDescription(task.TaskId, "123"), cancellation),
                "UpdateRepairTaskHealthPolicy" => repairs.UpdateRepairTaskHealthPolicyAsync(
                    new RepairTaskUpdateHealthPolicyDescription(task.TaskId, "123", true, false), cancellation),
                "UpdateRepairExecutionState" => repairs.UpdateRepairExecutionStateAsync(task, cancellation),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }
    }

    sealed class Request(HttpMethod method, Uri uri, string body)
    {
        internal readonly HttpMethod Method = method;
        internal readonly Uri Uri = uri;
        internal readonly string Body = body;
    }

    sealed class Recorder : DelegatingHandler
    {
        internal readonly ConcurrentQueue<Request> Requests = new();
        internal HttpStatusCode Status = HttpStatusCode.OK;
        internal string Response;
        internal Task Release = Task.CompletedTask;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            Requests.Enqueue(new Request(request.Method, request.RequestUri, body));
            await Release;
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Response ?? (request.Method == HttpMethod.Get ? "[]" : "{\"Version\":\"456\"}")),
            };
        }
    }
}
