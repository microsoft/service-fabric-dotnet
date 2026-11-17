// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System.Collections.Generic;
using System.Fabric;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Microsoft.ServiceFabric.Consumer;

sealed class ConsumerService(StatelessServiceContext context) : StatelessService(context)
{
    /// <inheritdoc />
    protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners() =>
        [new ServiceInstanceListener(static context => new KestrelCommunicationListener(
            context,
            static (url, _) => Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(web => web.UseKestrel().UseUrls(url)
                    .Configure(app => app.Run(static request => request.Response.WriteAsync("Service Fabric consumer"))))
                .Build()))];
}
