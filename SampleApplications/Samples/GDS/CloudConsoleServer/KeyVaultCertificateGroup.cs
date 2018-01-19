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

using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Opc.Ua.Gds.Server
{

    public class KeyVaultCertificateGroup : CertificateGroup
    {

        private KeyVaultCertificateGroup(
            string authoritiesStorePath,
            CertificateGroupConfiguration certificateGroupConfiguration):
            base(authoritiesStorePath, certificateGroupConfiguration)
        {
        }

        public KeyVaultCertificateGroup()
        {
        }

        #region ICertificateGroupProvider
        public override Task Init()
        {
            return Task.CompletedTask;
        }

        public override Task<X509Certificate2> NewKeyPairRequestAsync(
            ApplicationRecordDataType application, 
            string subjectName, 
            string[] domainNames, 
            string privateKeyFormat, 
            string privateKeyPassword)
        {
            return null;
        }

        public override Task RevokeCertificateAsync(
            X509Certificate2 certificate)
        {
            return Task.CompletedTask;
        }

        public override Task<X509Certificate2> SigningRequestAsync(
            ApplicationRecordDataType application,
            string[] domainNames,
            byte[] certificateRequest)
        {
            return null;
        }

        public override Task<X509Certificate2> CreateCACertificateAsync(
            string subjectName
            )
        {
            return null;
        }
        #endregion
    }

}
