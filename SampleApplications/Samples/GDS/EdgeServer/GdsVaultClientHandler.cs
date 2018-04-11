/* ========================================================================
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
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.IoTSolutions.OpcGdsVault.WebService.Client;
using Microsoft.Azure.IoTSolutions.OpcGdsVault.WebService.Client.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Opc.Ua.Gds.Server
{
    public class OpcGdsVaultConfig: IOpcGdsVaultConfig
    {
        public OpcGdsVaultConfig(string url)
        {
            OpcGdsVaultServiceApiUrl = url + "/v1";
        }
        public string OpcGdsVaultServiceApiUrl { get; }
    }

    public class OpcGdsVaultClientHandler
    {
        string _vaultBaseUrl;
        string _appId;
        IOpcGdsVaultClient _gdsVaultClient;
        IOpcGdsVaultConfig _gdsVaultConfig;
        ClientAssertionCertificate _assertionCert;

        public IOpcGdsVaultClient GdsVaultClient { get => _gdsVaultClient; }

        public OpcGdsVaultClientHandler(string vaultBaseUrl)
        {
            _vaultBaseUrl = vaultBaseUrl;
            _gdsVaultConfig = new OpcGdsVaultConfig(_vaultBaseUrl);
        }

        public void SetAssertionCertificate(
            string appId,
            X509Certificate2 clientAssertionCertPfx)
        {
            _appId = appId;
            _assertionCert = new ClientAssertionCertificate(appId, clientAssertionCertPfx);
            _gdsVaultClient = new OpcGdsVaultClient(
                _gdsVaultConfig,
                new AuthenticationCallback(GetAccessTokenAsync));
        }

        public void SetTokenProvider()
        {
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
            _gdsVaultClient = new OpcGdsVaultClient(
                _gdsVaultConfig,
                new AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
        }

        private async Task<string> GetAccessTokenAsync(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            var result = await context.AcquireTokenAsync(resource, _assertionCert);
            return result.AccessToken;
        }

        public async Task<string> GetIotHubSecretAsync()
        {
            var secret = await _gdsVaultClient.GetIotHubSecretAsync().ConfigureAwait(false);
            return secret.Secret;
        }

        public async Task<X509Certificate2Collection> GetCACertificateChainAsync(string id)
        {
            var result = new X509Certificate2Collection();
            var chainApiModel = await _gdsVaultClient.GetCACertificateChainAsync(id).ConfigureAwait(false);
            foreach (var certApiModel in chainApiModel.Chain)
            {
                var cert = new X509Certificate2(Convert.FromBase64String(certApiModel.Certificate));
                result.Add(cert);
            }
            return result;
        }

        public async Task<IList<Opc.Ua.X509CRL>> GetCACrlChainAsync(string id)
        {
            var result = new List<Opc.Ua.X509CRL>();
            var chainApiModel = await _gdsVaultClient.GetCACrlChainAsync(id).ConfigureAwait(false);
            foreach (var certApiModel in chainApiModel.Chain)
            {
                var crl = new Opc.Ua.X509CRL(Convert.FromBase64String(certApiModel.Crl));
                result.Add(crl);
            }
            return result;
        }

        public async Task<CertificateGroupConfigurationCollection> GetCertificateConfigurationGroupsAsync(string baseStorePath)
        {
            var groups = await _gdsVaultClient.GetCertificateGroupConfiguration().ConfigureAwait(false);
            var groupCollection = new CertificateGroupConfigurationCollection();
            foreach (var group in groups.Groups)
            {
                var newGroup = new CertificateGroupConfiguration()
                {
                    Id = group.Id,
                    SubjectName = group.SubjectName,
                    BaseStorePath = baseStorePath + Path.DirectorySeparatorChar + group.Id,
                    DefaultCertificateHashSize = group.DefaultCertificateHashSize,
                    DefaultCertificateKeySize = group.DefaultCertificateKeySize,
                    DefaultCertificateLifetime = group.DefaultCertificateLifetime
                };
                groupCollection.Add(newGroup);
            }
            return groupCollection;
        }

        public async Task<X509Certificate2> SigningRequestAsync(
            string id,
            ApplicationRecordDataType application,
            byte[] certificateRequest)
        {
            var sr = new SigningRequestApiModel()
            {
                ApplicationURI = application.ApplicationUri,
                Csr = Convert.ToBase64String(certificateRequest)
            };
            var certModel = await _gdsVaultClient.SigningRequestAsync(id, sr).ConfigureAwait(false);
            return new X509Certificate2(Convert.FromBase64String(certModel.Certificate));
        }

        public async Task<Opc.Ua.X509CRL> RevokeCertificateAsync(
            string id,
            X509Certificate2 certificate)
        {
            var certModel = new X509Certificate2ApiModel()
            {
                Certificate = Convert.ToBase64String(certificate.RawData),
                Subject = certificate.Subject,
                Thumbprint = certificate.Thumbprint
            };
            var crlModel = await _gdsVaultClient.RevokeCertificateAsync(id, certModel).ConfigureAwait(false);
            return new Opc.Ua.X509CRL(Convert.FromBase64String(crlModel.Crl));

        }

        public async Task<X509Certificate2KeyPair> NewKeyPairRequestAsync(
            string id,
            ApplicationRecordDataType application,
            string subjectName,
            string[] domainNames,
            string privateKeyFormat,
            string privateKeyPassword)
        {
            var certModel = new NewKeyPairRequestApiModel()
            {
                ApplicationURI = application.ApplicationUri,
                SubjectName = subjectName,
                DomainNames = domainNames,
                PrivateKeyFormat = privateKeyFormat,
                PrivateKeyPassword = privateKeyPassword
            };
            var nkpModel = await _gdsVaultClient.NewKeyPairRequestAsync(id, certModel).ConfigureAwait(false);
            return new X509Certificate2KeyPair(
                new X509Certificate2(Convert.FromBase64String(nkpModel.Certificate)),
                nkpModel.PrivateKeyFormat,
                Convert.FromBase64String(nkpModel.PrivateKey));
        }

#if mist
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


        public async Task CreateCACertificateAsync(
            string name, 
            string subjectName, 
            int keySize)
        {
            CertificateAttributes attributes = new CertificateAttributes { Enabled = true };

            var policy = new CertificatePolicy
            {
                IssuerParameters = new IssuerParameters
                {
                    Name = "Self",
                },
                KeyProperties = new KeyProperties
                {
                    Exportable = true,
                    KeySize = keySize,
                    KeyType = "RSA"
                },
                SecretProperties = new SecretProperties
                {
                    ContentType = CertificateContentType.Pem
                },
                X509CertificateProperties = new X509CertificateProperties
                {
                    Subject = subjectName
                }
            };

            var pendingCertificate = await _keyVaultClient.CreateCertificateAsync(
                _vaultBaseUrl, name, policy, attributes);
            // TODO: wait for operation
            var pendingCertificateResponse = await _keyVaultClient.GetCertificateOperationAsync(
                _vaultBaseUrl, pendingCertificate.CertificateOperationIdentifier.Name);
        }

#if testcode
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
#endif

        public async Task<KeyBundle> LoadSigningKeyAsync(string signingCertificateKey)
        {
            return await _keyVaultClient.GetKeyAsync(signingCertificateKey);
        }

        public async Task<X509Certificate2> LoadSigningCertificateAsync(string signingCertificateKey, X509Certificate2 publicCert)
        {
            var secret = await _keyVaultClient.GetSecretAsync(signingCertificateKey);
            if (secret.ContentType == CertificateContentType.Pfx)
            {
                var certBlob = Convert.FromBase64String(secret.Value);
                return CertificateFactory.CreateCertificateFromPKCS12(certBlob, string.Empty);
            }
            else if (secret.ContentType == CertificateContentType.Pem)
            {
                Encoding encoder = Encoding.UTF8;
                var privateKey = encoder.GetBytes(secret.Value.ToCharArray());
                return CertificateFactory.CreateCertificateWithPEMPrivateKey(publicCert, privateKey, string.Empty);
            }

            throw new NotImplementedException("Unknown content type: " + secret.ContentType);
        }

        public async Task SignDigestAsync(string signingKey, byte [] digest)
        {
            var result = await _keyVaultClient.SignAsync(signingKey, JsonWebKeySignatureAlgorithm.RS256, digest);
        }
#endif
    }
}

