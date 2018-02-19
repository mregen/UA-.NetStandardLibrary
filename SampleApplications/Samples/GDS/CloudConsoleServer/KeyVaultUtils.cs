﻿/* ========================================================================
 * Copyright (c) 2005-2017 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Opc.Ua.Gds.Server
{

    public class KeyVaultHandler
    {
        string _vaultBaseUrl;
        string _appId;
        IKeyVaultClient _keyVaultClient;
        ClientAssertionCertificate _assertionCert;

        public IKeyVaultClient KeyVaultClient { get => _keyVaultClient; }

        public KeyVaultHandler(string vaultBaseUrl)
        {
            _vaultBaseUrl = vaultBaseUrl;
        }

        public void SetAssertionCertificate(
            string appId,
            X509Certificate2 clientAssertionCertPfx)
        {
            _appId = appId;
            _assertionCert = new ClientAssertionCertificate(appId, clientAssertionCertPfx);
            _keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(GetAccessTokenAsync));
        }

        public void SetTokenProvider()
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
            _keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
        }

        private async Task<string> GetAccessTokenAsync(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            var result = await context.AcquireTokenAsync(resource, _assertionCert);
            return result.AccessToken;
        }

        public async Task<string> GetIotHubSecretAsync()
        {
            SecretBundle secret = await _keyVaultClient.GetSecretAsync(_vaultBaseUrl + "/secrets/iothub").ConfigureAwait(false);
            return secret.Value;
        }

        public async Task<CertificateBundle> GetCertificateAsync(string name)
        {
            return await _keyVaultClient.GetCertificateAsync(_vaultBaseUrl, name).ConfigureAwait(false);
        }

        public async Task<X509Certificate2Collection> GetCertificateVersionsAsync(string id)
        {
            var certificates = new X509Certificate2Collection();
            try
            {
                var certItems = await _keyVaultClient.GetCertificateVersionsAsync(_vaultBaseUrl, id, 3).ConfigureAwait(false);
                while (certItems != null)
                {
                    foreach (var certItem in certItems)
                    {
                        var certBundle = await _keyVaultClient.GetCertificateAsync(certItem.Id).ConfigureAwait(false);
                        var cert = new X509Certificate2(certBundle.Cer);
                        certificates.Add(cert);
                    }
                    if (certItems.NextPageLink != null)
                    {
                        certItems = await _keyVaultClient.GetCertificateVersionsNextAsync(certItems.NextPageLink).ConfigureAwait(false);
                    }
                    else
                    {
                        certItems = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Trace("Error while loading the certificate versions for " + id);
                Utils.Trace("Exception: " + ex.Message);
            }
            return certificates;
        }


        public async Task CreateCACertificateAsync(string name, string subjectName)
        {
            CertificatePolicy policy = new CertificatePolicy();
            CertificateAttributes attributes = new CertificateAttributes();

            await _keyVaultClient.CreateCertificateAsync(
                _vaultBaseUrl,
                name,
                policy,
                attributes
                );
        }

        public async Task<SecretBundle> ReadKeyWithCertAsync()
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

            try
            {
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetAccessTokenAsync));

                SecretBundle secret = await _keyVaultClient.GetSecretAsync(_vaultBaseUrl + "/secrets/secret")
                    .ConfigureAwait(false);

                var certlist = await _keyVaultClient.GetCertificatesAsync(_vaultBaseUrl);
                var result = await _keyVaultClient.GetCertificateAsync(_vaultBaseUrl, "Default").ConfigureAwait(false);
                var cert = new X509Certificate2(result.Cer);
                var secretvalue = $"Secret: {secret.Value}";
                string principal = azureServiceTokenProvider.PrincipalUsed != null ?
                    $"Principal Used: {azureServiceTokenProvider.PrincipalUsed}" :
                    string.Empty;
                return secret;
            }
            catch (Exception exp)
            {
                var error = $"Something went wrong: {exp.Message}";
            }

            return null;
        }

        /// <summary>
        /// load the authority signing key.
        /// </summary>
        public async Task<KeyBundle> LoadSigningKeyAsync(string signingCertificateKey, string signingKeyPassword)
        {
            return await _keyVaultClient.GetKeyAsync(signingCertificateKey);
        }

        public async Task<X509Certificate2> LoadSigningCertificateAsync(X509Certificate2 publicCert, string signingCertificateKey, string signingKeyPassword)
        {
            var secret = await _keyVaultClient.GetSecretAsync(signingCertificateKey);
            if (secret.ContentType == "application/x-pkcs12")
            {
                var certBlob = Convert.FromBase64String(secret.Value);
                return CertificateFactory.CreateCertificateFromPKCS12(certBlob, string.Empty);
            }
            // TODO: test the pem certs
            else if (secret.ContentType == "application/x-pem")
            {
                return CertificateFactory.CreateCertificateWithPEMPrivateKey(publicCert, null /*secret.Value.ToByteArray()*/, string.Empty);
            }

            throw new NotImplementedException("Can not decode Private Key with content type: " + secret.ContentType);
        }

    }
}

