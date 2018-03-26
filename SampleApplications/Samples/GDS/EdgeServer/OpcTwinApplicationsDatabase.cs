using Microsoft.Azure.IoTSolutions.Common.Diagnostics;
using Microsoft.Azure.IoTSolutions.Common.Exceptions;
using Microsoft.Azure.IoTSolutions.Common.Http;
using Microsoft.Azure.IoTSolutions.OpcTwin.WebService.Client;
using Microsoft.Azure.IoTSolutions.OpcTwin.WebService.Client.Models;
using Microsoft.Azure.IoTSolutions.OpcTwin.WebService.Client.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Opc.Ua.Gds.Server.Database
{

    public class OpcTwinConfig : IOpcTwinConfig
    {
        public string OpcTwinServiceApiUrl { get; }
        public OpcTwinConfig(string url)
        {
            OpcTwinServiceApiUrl = url;
        }
    }
    public class OpcTwinApplicationsDatabase : ApplicationsDatabaseBase
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
                foreach(var app in result)
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

        public override NodeId CreateCertificateRequest(
            NodeId applicationId,
            byte[] certificate,
            byte[] privateKey,
            string authorityId)
        {
            ValidateApplicationNodeId(applicationId);
#if mist
            // add cert and private key to device twin as tags
            Twin twin = _IoTHubDeviceRegistry.GetTwinAsync(id.ToString()).Result;
            if (twin != null)
            {
                CertificateRequestTwinRecord request = new CertificateRequestTwinRecord();
                request.RequestId = Guid.NewGuid();
                request.AuthorityId = authorityId;
                request.State = (int)CertificateRequestState.New;
                request.Certificate = certificate ?? new byte[0];
                request.PrivateKey = privateKey ?? new byte[0];

                string twinPatch = TwinPatchFromRecord(request);
                twin = _IoTHubDeviceRegistry.UpdateTwinAsync(twin.DeviceId, twinPatch, twin.ETag).Result;
                if (twin == null)
                {
                    throw new Exception("IoTHub certificate request failed.");
                }

                return new NodeId(request.RequestId, NamespaceIndex);
            }
            else
            {
                throw new Exception("IoTHub application id not found.");
            }
#endif
            return null;
        }

        public override void ApproveCertificateRequest(NodeId requestId, bool isRejected)
        {
            Guid reqId = GetNodeIdGuid(requestId);
#if mist
            string requestIdQuery = String.Format("SELECT * FROM devices WHERE tags.RequestId = '{0}'", id.ToString());
            var query = _IoTHubDeviceRegistry.CreateQuery(requestIdQuery, 10);
            bool done = false;
            while (query.HasMoreResults)
            {
                var page = query.GetNextAsTwinAsync().Result;
                foreach (var twin in page)
                {
                    CertificateRequestTwinRecord request = new CertificateRequestTwinRecord();
                    request.RequestId = id;
                    request.State = (int)((isRejected) ? CertificateRequestState.Rejected : CertificateRequestState.Approved);
                    string twinPatch = TwinPatchFromRecord(request);
                    var newTwin = _IoTHubDeviceRegistry.UpdateTwinAsync(twin.DeviceId, twinPatch, twin.ETag).Result;
                    if (newTwin == null)
                    {
                        throw new Exception("IoTHub approve certificate request failed.");
                    }
                    done = true;
                }
            }
            if (!done)
            {
                throw new ServiceResultException(StatusCodes.BadNodeIdUnknown);
            }
#endif
        }

        public override bool CompleteCertificateRequest(
            NodeId applicationId,
            NodeId requestId,
            out byte[] certificate,
            out byte[] privateKey)
        {
            certificate = null;
            privateKey = null;
            Guid reqId = GetNodeIdGuid(requestId);
            ValidateApplicationNodeId(applicationId);
#if mist
            Twin twin = _IoTHubDeviceRegistry.GetTwinAsync(appId.ToString()).Result;
            if ((twin != null) && (twin.Tags.Count > 0))
            {

                if (twin.Tags["RequestId"].Value != reqId.ToString())
                {
                    throw new ServiceResultException(StatusCodes.BadNodeIdUnknown);
                }

                if (twin.Tags["State"].Value == (int)CertificateRequestState.New)
                {
                    return false;
                }

                if (twin.Tags["State"].Value == (int)CertificateRequestState.Rejected)
                {
                    throw new ServiceResultException(StatusCodes.BadUserAccessDenied, "The certificate request has been rejected by the administrator.");
                }


                try
                {
                    string cert = string.Empty;
                    JObject certParts = (JObject)twin.Tags["Certificate"];
                    foreach (JToken part in certParts.Children())
                    {
                        cert += ((JProperty)part).Value;
                    }
                    certificate = Convert.FromBase64String(cert);
                    if (certificate.Length == 0)
                    {
                        privateKey = null;
                    }
                }
                catch (Exception)
                {
                    certificate = null;
                }

                try
                {
                    string pKey = string.Empty;
                    JObject pKeyParts = (JObject)twin.Tags["PrivateKey"];
                    foreach (JToken part in pKeyParts.Children())
                    {
                        pKey += ((JProperty)part).Value;
                    }
                    privateKey = Convert.FromBase64String(pKey);
                    if (privateKey.Length == 0)
                    {
                        privateKey = null;
                    }
                }
                catch (Exception)
                {
                    privateKey = null;
                }

                CertificateRequestTwinRecord request = new CertificateRequestTwinRecord();
                request.State = (int)CertificateRequestState.Accepted;
                string twinPatch = TwinPatchFromRecord(request);
                var newTwin = _IoTHubDeviceRegistry.UpdateTwinAsync(twin.DeviceId, twinPatch, twin.ETag).Result;
                if (newTwin == null)
                {
                    throw new Exception("IoTHub certificate accept failed.");
                }
            }
            else
            {
                throw new Exception("IoTHub twin retrieval failed");
            }
#endif
            return true;
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
                ApplicationName = applicationNamePattern ? null : applicationName,
                ApplicationUri = applicationUriPattern ? null : applicationUri,
                ProductUri = productUriPattern ? null : productUri,
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

