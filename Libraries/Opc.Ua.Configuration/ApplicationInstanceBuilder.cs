/* ========================================================================
 * Copyright (c) 2005-2020 The OPC Foundation, Inc. All rights reserved.
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
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Opc.Ua.Configuration
{
    /// <summary>
    /// A class that builds a configuration for a UA application.
    /// </summary>
    public class ApplicationInstanceBuilder :
        IApplicationInstanceBuilder
    {
        #region ctor
        /// <summary>
        /// Create the application instance builder.
        /// </summary>
        public ApplicationInstanceBuilder(ApplicationInstance applicationInstance)
        {
            ApplicationInstance = applicationInstance;
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// The application instance used to build the configuration.
        /// </summary>
        public ApplicationInstance ApplicationInstance { get; private set; }
        /// <summary>
        /// The application configuration.
        /// </summary>
        public ApplicationConfiguration ApplicationConfiguration => ApplicationInstance.ApplicationConfiguration;
        #endregion

        #region Public Methods
        /// <inheritdoc/>
        public IApplicationInstanceBuilderClientSelected AsClient()
        {
            switch (ApplicationInstance.ApplicationType)
            {
                case ApplicationType.Client:
                case ApplicationType.ClientAndServer:
                    break;
                case ApplicationType.Server:
                    ApplicationInstance.ApplicationType =
                        m_typeSelected ? ApplicationType.ClientAndServer : ApplicationType.Client;
                    break;
                default:
                    throw new ArgumentException("Invalid application type for client.");
            }

            m_typeSelected = true;

            ApplicationConfiguration.ClientConfiguration = new ClientConfiguration();

            return this;
        }

        /// <inheritdoc/>
        public IApplicationInstanceBuilderCreate AddSecurityConfiguration(
            string subjectName,
            string pkiRoot = null,
            string appRoot = null,
            string rejectedRoot = null
            )
        {
            appRoot = DefaultPKIRoot(appRoot, true);
            pkiRoot = DefaultPKIRoot(pkiRoot);
            rejectedRoot = DefaultPKIRoot(rejectedRoot);
            var appStoreType = CertificateStoreIdentifier.DetermineStoreType(appRoot);
            var pkiRootType = CertificateStoreIdentifier.DetermineStoreType(pkiRoot);
            var rejectedRootType = CertificateStoreIdentifier.DetermineStoreType(rejectedRoot);
            ApplicationConfiguration.SecurityConfiguration = new SecurityConfiguration {
                // app cert store
                ApplicationCertificate = new CertificateIdentifier() {
                    StoreType = appStoreType,
                    StorePath = DefaultCertificateStorePath(TrustlistType.Application, pkiRoot),
                    SubjectName = Utils.ReplaceDCLocalhost(subjectName)
                },
                // App trusted & issuer
                TrustedPeerCertificates = new CertificateTrustList() {
                    StoreType = pkiRootType,
                    StorePath = DefaultCertificateStorePath(TrustlistType.Trusted, pkiRoot)
                },
                TrustedIssuerCertificates = new CertificateTrustList() {
                    StoreType = pkiRootType,
                    StorePath = DefaultCertificateStorePath(TrustlistType.Issuer, pkiRoot)
                },
                // Https trusted & issuer
                TrustedHttpsCertificates = new CertificateTrustList() {
                    StoreType = pkiRootType,
                    StorePath = DefaultCertificateStorePath(TrustlistType.TrustedHttps, pkiRoot)
                },
                HttpsIssuerCertificates = new CertificateTrustList() {
                    StoreType = pkiRootType,
                    StorePath = DefaultCertificateStorePath(TrustlistType.IssuerHttps, pkiRoot)
                },
                // User trusted & issuer
                TrustedUserCertificates = new CertificateTrustList() {
                    StoreType = pkiRootType,
                    StorePath = DefaultCertificateStorePath(TrustlistType.TrustedUser, pkiRoot)
                },
                UserIssuerCertificates = new CertificateTrustList() {
                    StoreType = pkiRootType,
                    StorePath = DefaultCertificateStorePath(TrustlistType.IssuerUser, pkiRoot)
                },
                // rejected store
                RejectedCertificateStore = new CertificateTrustList() {
                    StoreType = rejectedRootType,
                    StorePath = DefaultCertificateStorePath(TrustlistType.Rejected, rejectedRoot)
                },
                // ensure secure default settings
                AutoAcceptUntrustedCertificates = false,
                AddAppCertToTrustedStore = false,
                RejectSHA1SignedCertificates = true,
                RejectUnknownRevocationStatus = true,
                SuppressNonceValidationErrors = false,
                SendCertificateChain = false,
                MinimumCertificateKeySize = CertificateFactory.DefaultKeySize
            };

            return this;
        }

        /// <inheritdoc/>
        public async Task<ApplicationConfiguration> Create()
        {
            // TODO: check applyTraceSettings
            if (/*applyTraceSettings && */ ApplicationConfiguration.TraceConfiguration != null)
            {
                ApplicationConfiguration.TraceConfiguration.ApplySettings();
            }

            // TODO
            // ApplicationConfiguration.SecurityConfiguration.CertificatePasswordProvider = certificatePasswordProvider;

            await ApplicationConfiguration.Validate(ApplicationInstance.ApplicationType).ConfigureAwait(false);

            await ApplicationConfiguration.CertificateValidator.
                Update(ApplicationConfiguration.SecurityConfiguration).ConfigureAwait(false);

            return ApplicationConfiguration;
        }

        /// <inheritdoc/>
        public IApplicationInstanceBuilderServerSelected AsServer(
            string[] baseAddresses,
            string[] alternateBaseAddresses = null)
        {
            switch (ApplicationInstance.ApplicationType)
            {
                case ApplicationType.Client:
                    ApplicationInstance.ApplicationType =
                        m_typeSelected ? ApplicationType.ClientAndServer : ApplicationType.Server;
                    break;
                case ApplicationType.Server:
                case ApplicationType.ClientAndServer: break;
                default:
                    throw new ArgumentException("Invalid application type for server.");
            }

            m_typeSelected = true;

            // configure a server
            var serverConfiguration = new ServerConfiguration();

            // by default disable LDS registration
            serverConfiguration.MaxRegistrationInterval = 0;

            // base addresses
            foreach (var baseAddress in baseAddresses)
            {
                serverConfiguration.BaseAddresses.Add(Utils.ReplaceLocalhost(baseAddress));
            }

            // alternate base addresses
            if (alternateBaseAddresses != null)
            {
                foreach (var alternateBaseAddress in alternateBaseAddresses)
                {
                    serverConfiguration.AlternateBaseAddresses.Add(Utils.ReplaceLocalhost(alternateBaseAddress));
                }
            }

            // add secure default policies
            serverConfiguration.SecurityPolicies = new ServerSecurityPolicyCollection();

            // add user token policy container and Anonymous
            serverConfiguration.UserTokenPolicies = new UserTokenPolicyCollection();
            serverConfiguration.UserTokenPolicies.Add(new UserTokenPolicy(UserTokenType.Anonymous));

            ApplicationConfiguration.ServerConfiguration = serverConfiguration;

            // add secure policies
            AddSecurityPolicies();

            return this;
        }

        /// <inheritdoc/>
        public IApplicationInstanceBuilderServerSelected AddUnsecurePolicyNone()
        {
            AddSecurityPolicies(false, false, true);
            return this;
        }

        /// <inheritdoc/>
        public IApplicationInstanceBuilderServerSelected AddSignPolicies(bool deprecated = false)
        {
            AddSecurityPolicies(true, deprecated, false);
            return this;
        }

        /// <inheritdoc/>
        public IApplicationInstanceBuilderServerSelected AddSignAndEncryptPolicies(bool deprecated = false)
        {
            AddSecurityPolicies(false, deprecated, false);
            return this;
        }

        /// <inheritdoc/>
        public IApplicationInstanceBuilderServerSelected AddUserTokenPolicy(UserTokenType userTokenType, bool clear = false)
        {
            if (clear)
            {
                ApplicationConfiguration.ServerConfiguration.UserTokenPolicies.Clear();
            }
            ApplicationConfiguration.ServerConfiguration.UserTokenPolicies.Add(new UserTokenPolicy(userTokenType));
            return this;
        }
        #endregion

        #region Private Methods
        private enum TrustlistType
        {
            Application,
            Trusted,
            Issuer,
            TrustedHttps,
            IssuerHttps,
            TrustedUser,
            IssuerUser,
            Rejected
        };

        private string DefaultPKIRoot(string root, bool appStore = false)
        {
            if (root == null ||
                root.Equals(CertificateStoreType.Directory, StringComparison.OrdinalIgnoreCase))
            {
                return CertificateStoreIdentifier.DefaultPKIRoot;
            }
            else if (root.Equals(CertificateStoreType.X509Store, StringComparison.OrdinalIgnoreCase))
            {
                return CertificateStoreIdentifier.CurrentUser;
            }
            return root;
        }

        private string DefaultCertificateStorePath(TrustlistType trustListType, string pkiRoot)
        {
            var pkiRootType = CertificateStoreIdentifier.DetermineStoreType(pkiRoot);
            if (pkiRootType == CertificateStoreType.Directory)
            {
                switch (trustListType)
                {
                    case TrustlistType.Application:
                        return pkiRoot + "/own";
                    case TrustlistType.Trusted:
                        return pkiRoot + "/trusted";
                    case TrustlistType.Issuer:
                        return pkiRoot + "/issuer";
                    case TrustlistType.TrustedHttps:
                        return pkiRoot + "/trustedHttps";
                    case TrustlistType.IssuerHttps:
                        return pkiRoot + "/issuerHttps";
                    case TrustlistType.TrustedUser:
                        return pkiRoot + "/trustedUser";
                    case TrustlistType.IssuerUser:
                        return pkiRoot + "/issuerUser";
                    case TrustlistType.Rejected:
                        return pkiRoot + "/rejected";
                }
            }
            else
            {
                switch (trustListType)
                {
                    case TrustlistType.Application:
#if !NETFRAMEWORK
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                            pkiRoot.StartsWith(CertificateStoreIdentifier.CurrentUser, StringComparison.OrdinalIgnoreCase))
                        {
                            return pkiRoot + "/My";
                        }
#endif
                        return pkiRoot + "UA_MachineDefault";
                    case TrustlistType.Trusted:
                        return pkiRoot + "UA_Trusted";
                    case TrustlistType.Issuer:
                        return pkiRoot + "UA_Issuer";
                    case TrustlistType.TrustedHttps:
                        return pkiRoot + "UA_Trusted_Https";
                    case TrustlistType.IssuerHttps:
                        return pkiRoot + "UA_Issuer_Https";
                    case TrustlistType.TrustedUser:
                        return pkiRoot + "UA_Trusted_User";
                    case TrustlistType.IssuerUser:
                        return pkiRoot + "UA_Issuer_User";
                    case TrustlistType.Rejected:
                        return pkiRoot + "UA_Rejected";
                }
            }
            throw new NotSupportedException("Unsupported store type.");
        }

        private void AddSecurityPolicies(bool includeSign = false, bool deprecated = false, bool policyNone = false)
        {
            // create list of supported policies
            string[] defaultPolicyUris = SecurityPolicies.GetDefaultUris();
            if (deprecated)
            {
                var names = SecurityPolicies.GetDisplayNames();
                var deprecatedPolicyList = new List<string>();
                foreach (var name in names)
                {
                    var uri = SecurityPolicies.GetUri(name);
                    if (uri != null)
                    {
                        deprecatedPolicyList.Add(uri);
                    }
                }
            }

            foreach (MessageSecurityMode securityMode in typeof(MessageSecurityMode).GetEnumValues())
            {
                var policies = ApplicationConfiguration.ServerConfiguration.SecurityPolicies;
                if (policyNone && securityMode == MessageSecurityMode.None)
                {
                    var newPolicy = new ServerSecurityPolicy() {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    };
                    if (policies.Find(s =>
                        s.SecurityMode == newPolicy.SecurityMode &&
                        string.Equals(s.SecurityPolicyUri, newPolicy.SecurityPolicyUri, StringComparison.Ordinal)
                        ) == null)
                    {
                        policies.Add(newPolicy);
                    }
                }
                else if (securityMode >= MessageSecurityMode.SignAndEncrypt ||
                    (includeSign && securityMode == MessageSecurityMode.Sign))
                {
                    foreach (var policyUri in defaultPolicyUris)
                    {
                        var newPolicy = new ServerSecurityPolicy() {
                            SecurityMode = securityMode,
                            SecurityPolicyUri = policyUri
                        };
                        if (policies.Find(s =>
                            s.SecurityMode == newPolicy.SecurityMode &&
                            string.Equals(s.SecurityPolicyUri, newPolicy.SecurityPolicyUri, StringComparison.Ordinal)
                            ) == null)
                        {
                            policies.Add(newPolicy);
                        }
                    }
                }

            }
        }
        #endregion

        #region Private Fields
        private bool m_typeSelected;
        #endregion
    }
}