// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.ServiceFabric.Client;

namespace Microsoft.ServiceFabric.Powershell.Http;

static class ExtendedServerValidationLoader
{
    const string AssemblyFileName = "ExtendedCertValidation.dll";
    const string TypeName = "Microsoft.ServiceFabric.Client.ExtendedCertValidation.ServerValidator";

    internal static Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?> Load()
    {
        string directory = Path.GetDirectoryName(typeof(ServiceFabricClientBuilder).Assembly.Location);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException(Resource.ErrorExtendedServerValidationUnavailable);

        string path = Path.Combine(directory, AssemblyFileName);
        try
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(Resource.ErrorExtendedServerValidationUnavailable, path);

            Assembly assembly = Assembly.LoadFrom(path);
            Type type = assembly.GetType(TypeName, false);
            ConstructorInfo constructor = type?.GetConstructor(Type.EmptyTypes);
            MethodInfo method = type?.GetMethod("Validate", BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(X509Certificate2), typeof(X509Chain), typeof(SslPolicyErrors), typeof(IReadOnlyCollection<string>) }, null);
            if (constructor == null || method == null || method.ReturnType != typeof(bool?) || method.ContainsGenericParameters)
                throw new MissingMethodException(TypeName, "ServerValidator()/Validate");

            object validator = constructor.Invoke(Array.Empty<object>());
            return (Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?>)method.CreateDelegate(
                typeof(Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?>), validator);
        }
        catch (Exception exception) when (exception is IOException || exception is BadImageFormatException ||
            exception is TypeLoadException || exception is MemberAccessException || exception is ArgumentException ||
            exception is TargetInvocationException || exception is System.Security.SecurityException)
        {
            throw new InvalidOperationException(Resource.ErrorExtendedServerValidationUnavailable, exception);
        }
    }
}
