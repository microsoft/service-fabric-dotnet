// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Services.Client;
using Moq;
using Moq.Protected;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Client;

public abstract class CommunicationClientFactoryBaseTest : IDisposable
{
    readonly CommunicationClientFactoryBase<ICommunicationClient> sut;

    // Constructor parameters
    readonly bool fireConnectEvents = fuzzy.Boolean();
    readonly IServicePartitionResolver servicePartitionResolver = Mock.Of<IServicePartitionResolver>();
    readonly IEnumerable<IExceptionHandler> exceptionHandlers = []; // Failures without handlers are thrown without retries
    readonly string traceId = fuzzy.String();

    readonly IProtectedAsMock<CommunicationClientFactoryBase<ICommunicationClient>, IProtectedMembers> protectedMembers;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    CommunicationClientFactoryBaseTest()
    {
        Mock<CommunicationClientFactoryBase<ICommunicationClient>> factory =
            new(fireConnectEvents, servicePartitionResolver, exceptionHandlers, traceId) { CallBase = true };
        sut = factory.Object;
        protectedMembers = factory.Protected().As<IProtectedMembers>();
    }

    void IDisposable.Dispose() =>
        sut.Dispose();

    public sealed class GetClientAsync_Uri_ServicePartitionKey_TargetReplicaSelector_String_OperationRetrySettings_CancellationToken : CommunicationClientFactoryBaseTest
    {
        new readonly ICommunicationClientFactory<ICommunicationClient> sut;

        // Method parameters
        readonly Uri serviceUri = fuzzy.Uri();
        readonly ServicePartitionKey partitionKey = new(fuzzy.Int64());
        readonly TargetReplicaSelector targetReplicaSelector = TargetReplicaSelector.RandomInstance; // Selects stateless endpoints
        readonly string listenerName = fuzzy.String();
        readonly OperationRetrySettings retrySettings = new();
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        readonly string endpoint = fuzzy.String();
        readonly ICommunicationClient client = Mock.Of<ICommunicationClient>();

        public GetClientAsync_Uri_ServicePartitionKey_TargetReplicaSelector_String_OperationRetrySettings_CancellationToken()
        {
            sut = base.sut;
            ResolvedServicePartition partition = StatelessPartition(new ServiceEndpointCollection(listenerName, endpoint).ToString());
            _ = Mock.Get(servicePartitionResolver)
                .Setup(_ => _.ResolveAsync(serviceUri, partitionKey, It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(partition);
            _ = protectedMembers.Setup(_ => _.CreateClientAsync(endpoint, It.IsAny<CancellationToken>())).ReturnsAsync(client);
        }

        [Fact]
        public async Task RethrowsClientCreationFailureThatOccurredWhileWaiting()
        {
            // Arrange
            TaskCompletionSource<bool> opening = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = protectedMembers.Setup(_ => _.OpenClient(client, It.IsAny<CancellationToken>())).Returns(opening.Task);
            _ = GetClient();
            Task<ICommunicationClient>[] waiting = fuzzy.Array(GetClient);
            Exception expected = new(fuzzy.String());

            // Act
            opening.SetException(expected);

            // Assert
            foreach (Task<ICommunicationClient> request in waiting)
                Assert.Same(expected, await Assert.ThrowsAsync<Exception>(() => request));
            protectedMembers.Verify(_ => _.CreateClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task CreatesClientWhenCreationFailedBeforeWaiting()
        {
            _ = protectedMembers.SetupSequence(_ => _.OpenClient(client, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(fuzzy.String()))
                .Returns(Task.CompletedTask);
            _ = await Assert.ThrowsAsync<Exception>(GetClient);

            ICommunicationClient actual = await GetClient();

            Assert.Same(client, actual);
        }

        [Fact]
        public async Task CreatesClientWhenCreationFailedBecauseItsRequestWasCanceled()
        {
            // Arrange
            using CancellationTokenSource cancellation = new();
            TaskCompletionSource<bool> opening = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = protectedMembers.SetupSequence(_ => _.OpenClient(client, It.IsAny<CancellationToken>()))
                .Returns(opening.Task)
                .Returns(Task.CompletedTask);
            _ = sut.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, cancellation.Token);
            Task<ICommunicationClient> waiting = GetClient();

            // Act
            cancellation.Cancel();
            opening.SetException(new Exception(fuzzy.String())); // Like FabricTransport, failing open after cancellation

            // Assert
            Assert.Same(client, await waiting);
        }

        Task<ICommunicationClient> GetClient() =>
            sut.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, cancellationToken);

        // Stateless endpoint avoids the native implementation of ResolvedServicePartition.GetEndpoint() used for primaries
        static ResolvedServicePartition StatelessPartition(string address)
        {
            ResolvedServiceEndpoint endpoint = Type<ResolvedServiceEndpoint>.New();
            endpoint.Property<ServiceEndpointRole>().Set(ServiceEndpointRole.Stateless);
            endpoint.Property<string>().Set(address);

            SingletonPartitionInformation info = new();
            info.Property<Guid>().Set(Guid.NewGuid());

            ResolvedServicePartition partition = Type<ResolvedServicePartition>.New();
            partition.Property<ICollection<ResolvedServiceEndpoint>>().Set(new List<ResolvedServiceEndpoint> { endpoint });
            partition.Property<ServicePartitionInformation>().Set(info);
            return partition;
        }
    }

    // Analog of the protected members of CommunicationClientFactoryBase<T> for Moq.Protected().As<T>()
    interface IProtectedMembers
    {
        Task<ICommunicationClient> CreateClientAsync(string endpoint, CancellationToken cancellationToken);

        Task OpenClient(ICommunicationClient client, CancellationToken cancellationToken);
    }
}
