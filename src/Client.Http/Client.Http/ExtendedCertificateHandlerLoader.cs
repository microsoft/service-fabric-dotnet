// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ServiceFabric.Client.Http;

static class ExtendedCertificateHandlerLoader
{
    // These names define a contract with an optional assembly, not a compile-time dependency.
    const string LibraryName = "Microsoft.ServiceFabric.Client.Http.Internal.dll";
    const string HandlerTypeName = "Microsoft.ServiceFabric.Client.Http.Internal.ExtendedCertificateHandler";
    const string MethodName = "ValidateCertificate";

    internal static Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?> Load() =>
        LoadFrom(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), LibraryName));

    internal static Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?> LoadFrom(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        // Missing dependencies and other load failures must not look like an absent optional module.
        Assembly module = Assembly.LoadFrom(path);
        return Bind(module.GetType(HandlerTypeName, throwOnError: true));
    }

    internal static Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?> Bind(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (!type.IsClass || !type.IsVisible)
            throw new TypeLoadException(string.Format(SR.ErrorExtendedCertificateHandlerType, type.FullName));

        Type[] parameters =
        [
            typeof(X509Certificate2), typeof(X509Chain), typeof(SslPolicyErrors), typeof(IReadOnlyCollection<string>)
        ];
        MethodInfo method = type.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.ExactBinding,
            binder: null, types: parameters, modifiers: null);
        if (method == null)
            throw new MissingMethodException(type.FullName, MethodName);

        object handler = Activator.CreateInstance(type);
        return (Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?>)
            method.CreateDelegate(typeof(Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?>), handler);
    }
}
