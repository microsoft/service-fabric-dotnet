// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Runtime;

FrameworkName framework = new(AppContext.TargetFrameworkName ??
    throw new InvalidOperationException("The consumer target framework is missing."));
if (Environment.Version.Major != framework.Version.Major || Environment.Version.Minor != framework.Version.Minor)
    throw new InvalidOperationException($"Expected runtime {framework.Version}, not {Environment.Version}.");

Guid expected = Guid.NewGuid();
ActorId actor = new(expected);
if (actor.GetGuidId() != expected)
    throw new InvalidOperationException("ActorId roundtrip failed.");

Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.RuntimeIdentifier}; " +
    RuntimeInformation.ProcessArchitecture);
foreach (Type type in new[] { typeof(StatelessService), typeof(ActorId), typeof(KestrelCommunicationListener), typeof(FabricClient) })
    Console.WriteLine($"{type.FullName}: {type.Assembly.Location}");

Console.WriteLine("Managed consumer smoke passed; no SF service registration or native Runtime operation performed.");
