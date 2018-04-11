/* ========================================================================
 * Copyright (c) 2005-2018 The OPC Foundation, Inc. All rights reserved.
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
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Opc.Ua.Gds.Server
{

    public class GdsVaultCertificateGroup : CertificateGroup
    {
        private OpcGdsVaultClientHandler _gdsVaultHandler;

        private GdsVaultCertificateGroup(
            OpcGdsVaultClientHandler gdsVaultHandler,
            string authoritiesStorePath,
            CertificateGroupConfiguration certificateGroupConfiguration) :
            base(authoritiesStorePath, certificateGroupConfiguration)
        {
            _gdsVaultHandler = gdsVaultHandler;
        }

        public GdsVaultCertificateGroup(OpcGdsVaultClientHandler gdsVaultHandler)
        {
            _gdsVaultHandler = gdsVaultHandler;
        }

        #region ICertificateGroupProvider
        public override CertificateGroup Create(
            string storePath,
            CertificateGroupConfiguration certificateGroupConfiguration)
        {
            return new GdsVaultCertificateGroup(_gdsVaultHandler, storePath, certificateGroupConfiguration);
        }

        public override async Task Init()
        {
            Utils.Trace(Utils.TraceMasks.Information, "InitializeCertificateGroup: {0}", m_subjectName);

            X509Certificate2Collection rootCACertificateChain;
            IList<Opc.Ua.X509CRL> rootCACrlChain;
            try
            {
                // read root CA chain for certificate group
                rootCACertificateChain = await _gdsVaultHandler.GetCACertificateChainAsync(Configuration.Id).ConfigureAwait(false);
                rootCACrlChain = await _gdsVaultHandler.GetCACrlChainAsync(Configuration.Id).ConfigureAwait(false);
                var rootCaCert = rootCACertificateChain[0];
                var rootCaCrl = rootCACrlChain[0];

                if (Utils.CompareDistinguishedName(rootCaCert.Subject, m_subjectName))
                {
                    Certificate = rootCaCert;
                    rootCaCrl.VerifySignature(rootCaCert, true);
                }
                else
                {
                    throw new ServiceResultException("Key Vault certificate subject(" + rootCaCert.Subject + ") does not match cert group subject " + m_subjectName);
                }
            }
            catch (Exception ex)
            {
                Utils.Trace("Failed to load CA certificate " + Configuration.Id + " from key Vault ");
                Utils.Trace(ex.Message);
                throw ex;
            }

            // add all existing cert versions to trust list

            // erase old certs
            using (ICertificateStore store = CertificateStoreIdentifier.OpenStore(m_authoritiesStorePath))
            {
                try
                {
                    X509Certificate2Collection certificates = await store.Enumerate();
                    foreach (var certificate in certificates)
                    {
                        // TODO: Subject may have changed over time
                        if (Utils.CompareDistinguishedName(certificate.Subject, m_subjectName))
                        {
                            if (null == rootCACertificateChain.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false))
                            {
                                Utils.Trace("Delete CA certificate from authority store: " + certificate.Thumbprint);

                                // delete existing CRL in trusted list
                                foreach (var crl in store.EnumerateCRLs(certificate, false))
                                {
                                    if (crl.VerifySignature(certificate, false))
                                    {
                                        store.DeleteCRL(crl);
                                    }
                                }

                                await store.Delete(certificate.Thumbprint);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.Trace("Failed to Delete existing certificates from authority store: " + ex.Message);
                }

                foreach (var rootCACertificate in rootCACertificateChain)
                {
                    X509Certificate2Collection certs = await store.FindByThumbprint(rootCACertificate.Thumbprint);
                    if (certs.Count == 0)
                    {
                        await store.Add(rootCACertificate);
                        Utils.Trace("Added CA certificate to authority store: " + rootCACertificate.Thumbprint);
                    }
                    else
                    {
                        Utils.Trace("CA certificate already exists in authority store: " + rootCACertificate.Thumbprint);
                    }

                    foreach (var rootCACrl in rootCACrlChain)
                    {
                        if (rootCACrl.VerifySignature(rootCACertificate, false))
                        {
                            // delete existing CRL in trusted list
                            foreach (var crl in store.EnumerateCRLs(rootCACertificate, false))
                            {
                                if (crl.VerifySignature(rootCACertificate, false))
                                {
                                    store.DeleteCRL(crl);
                                }
                            }

                            store.AddCRL(rootCACrl);
                        }
                    }
                }

                await UpdateAuthorityCertInTrustedList();

            }

        }

        public override async Task<X509Certificate2KeyPair> NewKeyPairRequestAsync(
            ApplicationRecordDataType application,
            string subjectName,
            string[] domainNames,
            string privateKeyFormat,
            string privateKeyPassword)
        {
            try
            {
                return await _gdsVaultHandler.NewKeyPairRequestAsync(
                    Configuration.Id,
                    application,
                    subjectName,
                    domainNames,
                    privateKeyFormat,
                    privateKeyPassword
                    ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is ServiceResultException)
                {
                    throw ex as ServiceResultException;
                }
                throw new ServiceResultException(StatusCodes.BadInvalidArgument, ex.Message);
            }
        }

        public override async Task<X509Certificate2> SigningRequestAsync(
            ApplicationRecordDataType application,
            string[] domainNames,
            byte[] certificateRequest)
        {
            try
            {
                var pkcs10CertificationRequest = new Org.BouncyCastle.Pkcs.Pkcs10CertificationRequest(certificateRequest);
                if (!pkcs10CertificationRequest.Verify())
                {
                    throw new ServiceResultException(StatusCodes.BadInvalidArgument, "CSR signature invalid.");
                }

                var info = pkcs10CertificationRequest.GetCertificationRequestInfo();
                var altNameExtension = GetAltNameExtensionFromCSRInfo(info);
                if (altNameExtension != null)
                {
                    if (altNameExtension.Uris.Count > 0)
                    {
                        if (!altNameExtension.Uris.Contains(application.ApplicationUri))
                        {
                            throw new ServiceResultException(StatusCodes.BadCertificateUriInvalid,
                                "CSR AltNameExtension does not match " + application.ApplicationUri);
                        }
                    }
                }
                return await _gdsVaultHandler.SigningRequestAsync(
                    Configuration.Id,
                    application,
                    certificateRequest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is ServiceResultException)
                {
                    throw ex as ServiceResultException;
                }
                throw new ServiceResultException(StatusCodes.BadInvalidArgument, ex.Message);
            }

        }


        public override async Task<Opc.Ua.X509CRL> RevokeCertificateAsync(
            X509Certificate2 certificate)
        {
            try
            {
                return await _gdsVaultHandler.RevokeCertificateAsync(
                    Configuration.Id,
                    certificate).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex is ServiceResultException)
                {
                    throw ex as ServiceResultException;
                }
                throw new ServiceResultException(StatusCodes.BadInvalidArgument, ex.Message);
            }
        }

        public override Task<X509Certificate2> CreateCACertificateAsync(
            string subjectName
            )
        {
            throw new NotImplementedException("CA creation not supported with key vault. Certificate is created and managed by keyVault administrator.");
        }

    }
#endregion
}
