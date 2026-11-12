// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.ServiceFabric.Common.Security;

namespace Microsoft.ServiceFabric.Client.Http
{
    sealed class HttpClientHandlerWrapper
    {
        readonly HttpClientHandler httpClientHandler;
        readonly Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?> extendedServerValidation;
        ServerCertificateValidatorHttpWrapper serverCertValidator;
        SecurityType securityType;
        bool securityConfigured;

        internal HttpClientHandlerWrapper(HttpClientHandler httpClientHandler,
            Func<X509Certificate2, X509Chain, SslPolicyErrors, IReadOnlyCollection<string>, bool?> extendedServerValidation = null)
        {
            this.httpClientHandler = httpClientHandler;
            this.extendedServerValidation = extendedServerValidation;
            securityConfigured = false;
        }

        internal void ConfigureSecuritySettings(SecuritySettings settings)
        {
            if (securityConfigured)
                throw new InvalidOperationException(SR.ErrorCannotConfigureSecurityAgain);

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (extendedServerValidation != null && settings.SecurityType != SecurityType.Claims && settings.SecurityType != SecurityType.X509)
                throw new InvalidOperationException(SR.ErrorExtendedServerValidationRequiresCertificateSecurity);

            securityType = settings.SecurityType;

            if (settings.SecurityType == SecurityType.Claims)
            {
                var claimsSettings = (ClaimsSecuritySettings)settings;
                serverCertValidator = new ServerCertificateValidatorHttpWrapper(claimsSettings.RemoteX509SecuritySettings,
                    extendedServerValidation);

                // If remote server cert validation fails, HttpClient throws HttpRequestException which has different exception information on full dotnet framework
                // and dotnet core, so ServerCertificateValidatorHttpWrapper.ValidateServerCertificate throws AuthenticationException which allows ServiceFabricHttpClient to detect it and make decisions.
                httpClientHandler.ServerCertificateCustomValidationCallback = serverCertValidator.ValidateServerCertificate;
            }
            else if (settings.SecurityType == SecurityType.X509)
            {
                var x509Settings = (X509SecuritySettings)settings;
                if (!httpClientHandler.ClientCertificates.Contains(x509Settings.ClientCertificate))
                    httpClientHandler.ClientCertificates.Add(x509Settings.ClientCertificate);

                serverCertValidator = new ServerCertificateValidatorHttpWrapper(x509Settings.RemoteX509SecuritySettings,
                    extendedServerValidation);

                // If remote server cert validation fails, HttpClient throws HttpRequestException which has different exception information on full dotnet framework
                // and dotnet core, so ServerCertificateValidatorHttpWrapper.ValidateServerCertificate throws AuthenticationException which allows ServiceFabricHttpClient to detect it and make decisions.
                httpClientHandler.ServerCertificateCustomValidationCallback = serverCertValidator.ValidateServerCertificate;
            }
            else if (settings.SecurityType == SecurityType.Windows)
                httpClientHandler.UseDefaultCredentials = true;

            securityConfigured = true;
        }

        internal void RefreshSecuritySettings(SecuritySettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (!securityConfigured)
                throw new InvalidOperationException(SR.ErrorCannotCallRefreshSecuritySettingsBeforeConfiguring);    

            if (securityType != settings.SecurityType)
                throw new InvalidOperationException(SR.ErrorCannotChangeSecurityType);

            if (settings.SecurityType == SecurityType.Claims)
            {
                // Update RemoteX509SecuritySettings for cert validator.
                var claimsSettings = (ClaimsSecuritySettings)settings;
                serverCertValidator.UpdateSecuritySettings(claimsSettings.RemoteX509SecuritySettings);
            }
            else if (settings.SecurityType == SecurityType.X509)
            {
                // Add new client cert and update RemoteX509SecuritySettings for cert validator.
                var x509Settings = (X509SecuritySettings)settings;
                httpClientHandler.ClientCertificates.Clear();
                if (!httpClientHandler.ClientCertificates.Contains(x509Settings.ClientCertificate))
                    httpClientHandler.ClientCertificates.Add(x509Settings.ClientCertificate);
                serverCertValidator.UpdateSecuritySettings(x509Settings.RemoteX509SecuritySettings);
            }
        }
    }
}
