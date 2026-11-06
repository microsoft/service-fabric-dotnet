// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ServiceFabric.Client.Http
{
    /// <summary>
    /// Extensions for IServiceFabricClientBuilder for configuring ServiceFabricHttpClient.
    /// </summary>
    public static class ServiceFabricClientBuilderExtensions
    {
        /// <summary>Configures additional server certificate validation.</summary>
        /// <param name="builder">The client builder.</param>
        /// <param name="callback">A callback returning true to accept, false to reject, or null to use the existing validator.</param>
        /// <returns>The client builder.</returns>
        /// <remarks>The callback receives the current configured server common names as a read-only snapshot.
        /// Calls can run concurrently. The callback must not dispose or retain the certificate or chain.
        /// Exceptions fail validation without invoking the existing validator.</remarks>
        /// <exception cref="ArgumentNullException">The builder or callback is null.</exception>
        public static ServiceFabricClientBuilder UseExtendedServerValidation(
            this ServiceFabricClientBuilder builder,
            Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?> callback)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            builder.Container[typeof(Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?>)] = callback;
            return builder;
        }

        /// <summary>
        /// Configure IServiceFabricClientBuilder to create IServiceFabricClient implementation which communicates with Service Fabric cluster via the REST http gateway using the provided HttpClientHandler and DelegatingHandlers.
        /// </summary>
        /// <param name="builder">Builder </param>
        /// <param name="innerHandler">The inner handler which is responsible for processing the HTTP response messages. When null or not provided, <see cref="System.Net.Http.HttpClientHandler"/> will be used as last handler in channel.</param>
        /// <param name="delegateHandlers">An ordered list of <see cref="System.Net.Http.DelegatingHandler"/> instances to be invoked in HTTP message channel as message flows to and from the last handler in the channel.</param>
        /// <returns>IServiceFabricClientBuilder instance.</returns>
        public static ServiceFabricClientBuilder ConfigureHttpClientSettings(this ServiceFabricClientBuilder builder, HttpClientHandler innerHandler, params DelegatingHandler[] delegateHandlers)
        {
            if (innerHandler == null)
                throw new ArgumentNullException(nameof(innerHandler));

            if (delegateHandlers.Any(handler => handler == null))
                throw new ArgumentException(SR.ErrorNullDelegateHandler);

            builder.Container[typeof(HttpClientHandler)] = innerHandler;
            builder.Container[typeof(DelegatingHandler[])] = delegateHandlers;
            return builder;
        }
    }
}
