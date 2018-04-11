using Microsoft.Azure.IoTSolutions.Common.Diagnostics;
using Microsoft.Azure.IoTSolutions.Common.Exceptions;
using Microsoft.Azure.IoTSolutions.Common.Http;
using Microsoft.Azure.IoTSolutions.OpcTwin.WebService.Client;
using Microsoft.Azure.IoTSolutions.OpcTwin.WebService.Client.Models;
using Microsoft.Azure.IoTSolutions.OpcTwin.WebService.Client.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Opc.Ua.Gds.Server.Database.OpcTwin
{

    public class OpcTwinConfig : IOpcTwinConfig
    {
        public string OpcTwinServiceApiUrl { get; }
        public OpcTwinConfig(string url)
        {
            OpcTwinServiceApiUrl = url;
        }
    }
    public class OpcTwinApplicationsDatabase : ApplicationsDatabaseBase, ICertificateRequest
    {
        IOpcTwinService _opcTwinServiceHandler = null;
        IHttpClient _httpClient = null;
        ILogger _logger = null;

        public OpcTwinApplicationsDatabase(string databaseStorePath, bool clean = true)
        {
            _logger = new Logger("OpcTwin", LogLevel.Debug);
            _httpClient = new HttpClient(_logger);
            _opcTwinServiceHandler = new OpcTwinServiceClient(_httpClient, new OpcTwinConfig(databaseStorePath), _logger);
            var result = _opcTwinServiceHandler.ListAllApplicationsAsync().Result;
            if (clean)
            {
                foreach (var app in result)
                {
                    _opcTwinServiceHandler.UnregisterApplicationAsync(app.ApplicationId).Wait();
                }
            }
        }

        public override NodeId RegisterApplication(
            ApplicationRecordDataType application
            )
        {
            NodeId appNodeId = base.RegisterApplication(application);
            string applicationId = GetNodeIdString(appNodeId);

            if (String.IsNullOrEmpty(applicationId))
            {
                var request = new ApplicationRegistrationRequestApiModel()
                {
                    ApplicationUri = application.ApplicationUri,
                    ApplicationName = application.ApplicationNames[0].Text,
                    ProductUri = application.ProductUri,
                    ApplicationType = (Microsoft.Azure.IoTSolutions.OpcTwin.WebService.Client.Models.ApplicationType?)application.ApplicationType,
                    Capabilities = application.ServerCapabilities,
                    DiscoveryUrls = application.DiscoveryUrls
                };

                try
                {
                    var response = _opcTwinServiceHandler.RegisterAsync(request).Result;
                    return new NodeId(response.Id, NamespaceIndex);
                }
                catch (ConflictingResourceException cre)
                {
                    // resource already exists, fall through and patch
                    // _logger.Debug(cre.Message);
                }
                catch (Exception e)
                {
                    throw e;
                }
            }

            if (String.IsNullOrEmpty(applicationId))
            {
                var request = new ApplicationRegistrationQueryApiModel()
                {
                    ApplicationUri = application.ApplicationUri
                };

                var response = _opcTwinServiceHandler.QueryApplicationsAsync(request).Result;
                if (response.Items.Count > 0)
                {
                    applicationId = response.Items[0].ApplicationId;
                }
            }

            if (!String.IsNullOrEmpty(applicationId))
            {
                var request = new ApplicationRegistrationUpdateApiModel()
                {
                    Id = applicationId,
                    // TODO: handle case when new app uri was assigned?
                    // ApplicationUri = application.ApplicationUri,
                    ApplicationName = application.ApplicationNames[0].Text,
                    ProductUri = application.ProductUri,
                    // TODO: handle change?
                    //ApplicationType = (Microsoft.Azure.IoTSolutions.OpcTwin.WebService.Client.Models.ApplicationType?)application.ApplicationType,
                    Capabilities = application.ServerCapabilities,
                    DiscoveryUrls = application.DiscoveryUrls,
                    Certificate = null,
                    DiscoveryProfileUri = null
                };

                _opcTwinServiceHandler.UpdateApplicationAsync(request).Wait();
            }

            return new NodeId(applicationId, NamespaceIndex);
        }

        public override void UnregisterApplication(
            NodeId applicationId,
            out byte[] certificate,
            out byte[] httpsCertificate)
        {
            certificate = null;
            httpsCertificate = null;
            _opcTwinServiceHandler.UnregisterApplicationAsync(GetNodeIdString(applicationId));
        }

        public override ApplicationRecordDataType GetApplication(NodeId applicationId)
        {
            ValidateApplicationNodeId(applicationId);
            var result = _opcTwinServiceHandler.GetApplicationAsync(GetNodeIdString(applicationId)).Result;
            return ToApplicationRecord(result.Application);
        }

        public override ApplicationRecordDataType[] FindApplications(string applicationUri)
        {
            try
            {
                var records = new List<ApplicationRecordDataType>();
                var request = new ApplicationRegistrationQueryApiModel()
                {
                    ApplicationUri = applicationUri
                };

                var result = _opcTwinServiceHandler.QueryApplicationsAsync(request).Result;
                if (result.Items.Count > 0)
                {
                    foreach (var record in result.Items)
                    {
                        records.Add(ToApplicationRecord(record));
                    }
                    return records.ToArray();
                }
            }
            catch
            {
                // intentionally continue
                // return null instead of exception if an error occurred 
            }
            return null;
        }

        public override ServerOnNetwork[] QueryServers(
            uint startingRecordId,
            uint maxRecordsToReturn,
            string applicationName,
            string applicationUri,
            string productUri,
            string[] serverCapabilities,
            out DateTime lastCounterResetTime)
        {
            lastCounterResetTime = DateTime.UtcNow;
            List<ServerOnNetwork> records = new List<ServerOnNetwork>();

            bool applicationNamePattern = IsMatchPattern(applicationName);
            bool applicationUriPattern = IsMatchPattern(applicationUri);
            bool productUriPattern = IsMatchPattern(productUri);

            var request = new ApplicationRegistrationQueryApiModel()
            {
                ApplicationName = applicationNamePattern || String.IsNullOrEmpty(applicationName) ? null : applicationName,
                ApplicationUri = applicationUriPattern || String.IsNullOrEmpty(applicationUri) ? null : applicationUri,
                ProductUri = productUriPattern || String.IsNullOrEmpty(productUri) ? null : productUri,
                Capabilities = serverCapabilities != null ? new List<string>(serverCapabilities) : null
            };

            var result = _opcTwinServiceHandler.QueryApplicationsAsync(request).Result;
            if (result.Items.Count > startingRecordId)
            {
                if (startingRecordId > 0)
                {
                    result.Items.RemoveRange(0, (int)startingRecordId - 1);
                }
                uint id = startingRecordId;
                foreach (var item in result.Items)
                {
                    ServerOnNetwork server = new ServerOnNetwork();
                    server.RecordId = id++;
                    ApplicationRecordDataType record = ToApplicationRecord(item);
                    if (record.ApplicationType == ApplicationType.Client)
                    {
                        continue;
                    }
                    if (applicationNamePattern && !Match(record.ApplicationNames[0].Text, applicationName))
                    {
                        continue;
                    }
                    if (applicationUriPattern && !Match(record.ApplicationUri, applicationUri))
                    {
                        continue;
                    }
                    if (productUriPattern && !Match(record.ProductUri, productUri))
                    {
                        continue;
                    }

                    server.ServerName = record.ApplicationNames[0].Text;
                    if (record.DiscoveryUrls.Count > 0)
                    {
                        server.DiscoveryUrl = record.DiscoveryUrls[0];
                    }
                    else
                    {
                        server.DiscoveryUrl = string.Empty;
                    }
                    server.ServerCapabilities = record.ServerCapabilities;
                    records.Add(server);

                    if (maxRecordsToReturn != 0 && records.Count >= maxRecordsToReturn)
                    {
                        break;
                    }
                }
            }
            return records.ToArray();
        }

        #region ICertificateRequest
        [Serializable]
        class CertificateRequest
        {
            public Guid RequestId { get; set; }
            public string ApplicationId { get; set; }
            public int State { get; set; }
            public NodeId CertificateGroupId { get; set; }
            public NodeId CertificateTypeId { get; set; }
            public byte[] CertificateSigningRequest { get; set; }
            public string SubjectName { get; set; }
            public string[] DomainNames { get; set; }
            public string PrivateKeyFormat { get; set; }
            public string PrivateKeyPassword { get; set; }
            public string AuthorityId { get; set; }
        }

        [JsonProperty]
        private ICollection<CertificateRequest> CertificateRequests = new HashSet<CertificateRequest>();
        private object Lock = new object();

        public NodeId CreateSigningRequest(
            NodeId applicationId,
            NodeId certificateGroupId,
            NodeId certificateTypeId,
            byte[] certificateRequest,
            string authorityId)
        {
            var application = GetApplication(applicationId);
            if (application == null)
            {
                throw new ServiceResultException(StatusCodes.BadNodeIdUnknown);
            }

            lock (Lock)
            {
                var id = GetNodeIdString(application.ApplicationId);
                var request = (from x in CertificateRequests where x.AuthorityId == authorityId && x.ApplicationId == id select x).SingleOrDefault();

                bool isNew = false;
                if (request == null)
                {
                    request = new CertificateRequest() { RequestId = Guid.NewGuid(), AuthorityId = authorityId };
                    isNew = true;
                }

                request.State = (int)CertificateRequestState.New;
                request.CertificateGroupId = certificateGroupId;
                request.CertificateTypeId = certificateTypeId;
                request.SubjectName = null;
                request.DomainNames = null;
                request.PrivateKeyFormat = null;
                request.PrivateKeyPassword = null;
                request.CertificateSigningRequest = certificateRequest;
                request.ApplicationId = id;

                if (isNew)
                {
                    CertificateRequests.Add(request);
                }
                return new NodeId(request.RequestId, NamespaceIndex);
            }
        }

        public NodeId CreateNewKeyPairRequest(
            NodeId applicationId,
            NodeId certificateGroupId,
            NodeId certificateTypeId,
            string subjectName,
            string[] domainNames,
            string privateKeyFormat,
            string privateKeyPassword,
            string authorityId)
        {
            var application = GetApplication(applicationId);
            if (application == null)
            {
                throw new ServiceResultException(StatusCodes.BadNodeIdUnknown);
            }

            lock (Lock)
            {
                var id = GetNodeIdString(application.ApplicationId);
                var request = (from x in CertificateRequests where x.AuthorityId == authorityId && x.ApplicationId == id select x).SingleOrDefault();

                bool isNew = false;

                if (request == null)
                {
                    request = new CertificateRequest()
                    {
                        RequestId = Guid.NewGuid(),
                        AuthorityId = authorityId
                    };
                    isNew = true;
                }

                request.State = (int)CertificateRequestState.New;
                request.CertificateGroupId = certificateGroupId;
                request.CertificateTypeId = certificateTypeId;
                request.SubjectName = subjectName;
                request.DomainNames = domainNames;
                request.PrivateKeyFormat = privateKeyFormat;
                request.PrivateKeyPassword = privateKeyPassword;
                request.CertificateSigningRequest = null;
                request.ApplicationId = id;

                if (isNew)
                {
                    CertificateRequests.Add(request);
                }

                return new NodeId(request.RequestId, NamespaceIndex);
            }
        }

        public void ApproveCertificateRequest(
            NodeId requestId,
            bool isRejected
            )
        {
            Guid id = GetNodeIdGuid(requestId);

            lock (Lock)
            {
                var request = (from x in CertificateRequests where x.RequestId == id select x).SingleOrDefault();

                if (request == null)
                {
                    throw new ServiceResultException(StatusCodes.BadNodeIdUnknown);
                }

                if (isRejected)
                {
                    request.State = (int)CertificateRequestState.Rejected;
                    // erase information which is ot required anymore
                    request.CertificateSigningRequest = null;
                    request.PrivateKeyPassword = null;
                }
                else
                {
                    request.State = (int)CertificateRequestState.Approved;
                }
            }
        }

        public void AcceptCertificateRequest(NodeId requestId)
        {
            Guid id = GetNodeIdGuid(requestId);

            lock (Lock)
            {
                var request = (from x in CertificateRequests where x.RequestId == id select x).SingleOrDefault();

                if (request == null)
                {
                    throw new ServiceResultException(StatusCodes.BadNodeIdUnknown);
                }

                request.State = (int)CertificateRequestState.Accepted;

                // erase information which is ot required anymore
                request.CertificateSigningRequest = null;
                request.PrivateKeyPassword = null;

            }
        }


        public CertificateRequestState CompleteCertificateRequest(
            NodeId applicationId,
            NodeId requestId,
            out NodeId certificateGroupId,
            out NodeId certificateTypeId,
            out byte[] certificateRequest,
            out string subjectName,
            out string[] domainNames,
            out string privateKeyFormat,
            out string privateKeyPassword)
        {
            certificateGroupId = null;
            certificateTypeId = null;
            certificateRequest = null;
            subjectName = null;
            domainNames = null;
            privateKeyFormat = null;
            privateKeyPassword = null;

            Guid reqId = GetNodeIdGuid(requestId);
            string appId = GetNodeIdString(applicationId);

            lock (Lock)
            {
                var request = (from x in CertificateRequests where x.RequestId == reqId select x).SingleOrDefault();

                if (request == null)
                {
                    throw new ServiceResultException(StatusCodes.BadInvalidArgument);
                }

                switch (request.State)
                {
                    case (int)CertificateRequestState.New:
                        return CertificateRequestState.New;
                    case (int)CertificateRequestState.Rejected:
                        return CertificateRequestState.Rejected;
                    case (int)CertificateRequestState.Accepted:
                        return CertificateRequestState.Accepted;
                    case (int)CertificateRequestState.Approved:
                        break;
                    default:
                        throw new ServiceResultException(StatusCodes.BadInvalidArgument);
                }

                certificateGroupId = request.CertificateGroupId;
                certificateTypeId = request.CertificateTypeId;
                certificateRequest = request.CertificateSigningRequest;
                subjectName = request.SubjectName;
                domainNames = request.DomainNames;
                privateKeyFormat = request.PrivateKeyFormat;
                privateKeyPassword = request.PrivateKeyPassword;

                return CertificateRequestState.Approved;
            }
        }
        #endregion


        private ApplicationRecordDataType ToApplicationRecord(ApplicationInfoApiModel record)
        {
            return new ApplicationRecordDataType()
            {
                ApplicationId = new NodeId(record.ApplicationId, NamespaceIndex),
                ApplicationNames = new LocalizedTextCollection() { new LocalizedText("en-us", record.ApplicationName) },
                ApplicationType = (Ua.ApplicationType)record.ApplicationType,
                ApplicationUri = record.ApplicationUri,
                DiscoveryUrls = record.DiscoveryUrls != null ? new StringCollection(record.DiscoveryUrls) : null,
                ProductUri = record.ProductUri,
                ServerCapabilities = record.Capabilities != null ? new StringCollection(record.Capabilities) : null
            };
        }

    }
}

