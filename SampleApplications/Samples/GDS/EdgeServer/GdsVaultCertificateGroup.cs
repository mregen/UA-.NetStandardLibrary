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
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Opc.Ua.Gds.Server
{

    public class GdsVaultCertificateGroup : CertificateGroup
    {
        private OpcGdsVaultClientHandler _gdsVaultHandler;
        // CA cert private key and pfx location
        private string _caCertSecretIdentifier;
        private string _caCertKeyIdentifier;

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

        public override Task Init()
        {
            Utils.Trace(Utils.TraceMasks.Information, "InitializeCertificateGroup: {0}", m_subjectName);

#if mist
            try
            {
                var result = await _gdsVaultHandler.GetCertificateAsync(Configuration.Id).ConfigureAwait(false);
                var cloudCert = new X509Certificate2(result.Cer);
                if (Utils.CompareDistinguishedName(cloudCert.Subject, m_subjectName))
                {
                    Certificate = cloudCert;
                    _caCertSecretIdentifier = result.SecretIdentifier.Identifier;
                    _caCertKeyIdentifier = result.KeyIdentifier.Identifier;
                    await _gdsVaultHandler.LoadSigningCertificateAsync(_caCertSecretIdentifier, Certificate);
                    //await _keyVaultHandler.SignDigestAsync(_caCertKeyIdentifier, digest);
                }
                else
                {
                    throw new ServiceResultException("Key Vault certificate subject(" + cloudCert.Subject + ") does not match cert group subject " + m_subjectName);
                }
            }
            catch (Exception ex)
            {
                Utils.Trace("Failed to load CA certificate " + Configuration.Id + " from key Vault ");
                Utils.Trace(ex.Message);
                throw ex;
            }

            // add all existing cert versions for trust list
            var allCerts = await _gdsVaultHandler.GetCertificateVersionsAsync(Configuration.Id);

            // erase old certs
            using (ICertificateStore store = CertificateStoreIdentifier.OpenStore(m_authoritiesStorePath))
            {
                try
                {
                    X509Certificate2Collection certificates = await store.Enumerate();
                    foreach (var certificate in certificates)
                    {
                        if (Utils.CompareDistinguishedName(certificate.Subject, m_subjectName))
                        {
                            if (null == allCerts.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false))
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

                foreach (var certificate in allCerts)
                {
                    X509Certificate2Collection certs = await store.FindByThumbprint(certificate.Thumbprint);
                    if (certs.Count == 0)
                    {
                        await store.Add(certificate);
                        Utils.Trace("Added CA certificate to authority store: " + certificate.Thumbprint);
                    }
                    else
                    {
                        Utils.Trace("CA certificate already exists in authority store: " + certificate.Thumbprint);
                    }
                }

                await UpdateAuthorityCertInTrustedList();
            }
#endif
            //throw new NotImplementedException("CA creation not supported with key vault. Certificate is created and managed by keyVault administrator.");
            return Task.CompletedTask;
        }

        public override Task RevokeCertificateAsync(
            X509Certificate2 certificate)
        {
            // revocation is not yet supported
            return Task.CompletedTask;
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
