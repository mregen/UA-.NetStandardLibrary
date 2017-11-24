using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Opc.Ua.Gds.Server.Database
{
    public class IoTHubApplicationsDatabase : ApplicationsDatabaseBase
    {
        RegistryManager _IoTHubDeviceRegistry = null;

        public IoTHubApplicationsDatabase()
        {
            //TODO: Make the IoT Hub connection string configurable
            _IoTHubDeviceRegistry = RegistryManager.CreateFromConnectionString(
                "HostName=iopgds.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=/s6sIO3+Bm0V6SlOaU8Y9sW4Subq/JsgEalXWu/msyU="
                        );

            var query = _IoTHubDeviceRegistry.CreateQuery("SELECT * FROM devices", 100);
            while (query.HasMoreResults)
            {
                var page = query.GetNextAsTwinAsync().Result;
                foreach (var twin in page)
                {
                    // do work on twin object
                    Utils.Trace(twin.DeviceId);
                }
            }
        }

        public override NodeId RegisterApplication(ApplicationRecordDataType application)
        {

            NodeId appNodeId = base.RegisterApplication(application);
            Guid applicationId = GetNodeIdGuid(appNodeId);
            string capabilities = base.ServerCapabilities(application);

            // check if we have a record already and create one if not
            Task<Device> t = _IoTHubDeviceRegistry.GetDeviceAsync(application.ApplicationUri);
            t.Wait();
            Device device = t.Result;

            if (device == null)
            {
                t = _IoTHubDeviceRegistry.AddDeviceAsync(new Device(application.ApplicationUri));
                t.Wait();
                device = t.Result;
                if (device == null)
                {
                    throw new Exception("IoTHub device creation failed");
                }
            }

            Task<Twin> t2 = _IoTHubDeviceRegistry.GetTwinAsync(application.ApplicationUri);
            t2.Wait();
            Twin twin = t2.Result;
            if (twin != null)
            {
                string patch = "{ tags: { ";

                patch += "ApplicationUri: " + JsonConvert.SerializeObject(application.ApplicationUri) + ",";

                patch += "ApplicationType: " + JsonConvert.SerializeObject(application.ApplicationType) + ",";

                patch += "ApplicationNames: {";
                int i = 0;
                for (i = 0; i < application.ApplicationNames.Count - 1; i++)
                {
                    patch += (i.ToString() + ": " + JsonConvert.SerializeObject(application.ApplicationNames[i].Text) + ",");
                }
                patch += (i.ToString() + ": " + JsonConvert.SerializeObject(application.ApplicationNames[i].Text));
                patch += "},";

                patch += "DiscoveryUrls: {";
                for (i = 0; i < application.DiscoveryUrls.Count - 1; i++)
                {
                    patch += (i.ToString() + ": " + JsonConvert.SerializeObject(application.DiscoveryUrls[i]) + ",");
                }
                if (i > 0)
                {
                    patch += (i.ToString() + ": " + JsonConvert.SerializeObject(application.DiscoveryUrls[i]));
                }
                patch += "},";

                patch += "ProductUri: " + JsonConvert.SerializeObject(application.ProductUri) + ",";

                patch += "ServerCapabilities: {";
                for (i = 0; i < application.ServerCapabilities.Count - 1; i++)
                {
                    patch += (i.ToString() + ": " + JsonConvert.SerializeObject(application.ServerCapabilities[i]) + ",");
                }
                if (i > 0)
                {
                    patch += (i.ToString() + ": " + JsonConvert.SerializeObject(application.ServerCapabilities[i]));
                }
                patch += "}";

                patch += "} }";

                t2 = _IoTHubDeviceRegistry.UpdateTwinAsync(twin.DeviceId, patch, twin.ETag);
                t2.Wait();
                twin = t2.Result;
                if (twin == null)
                {
                    throw new Exception("IoTHub device update failed");
                }
            }
            else
            {
                throw new Exception("IoTHub twin retrieval failed");
            }

            return new NodeId(application.ApplicationUri, NamespaceIndex);
        }

        private enum CertificateRequestState
        {
            New,
            Approved,
            Rejected,
            Accepted
        }

        public override NodeId CreateCertificateRequest(
            NodeId applicationId,
            byte[] certificate,
            byte[] privateKey,
            string authorityId)
        {
            if (NodeId.IsNull(applicationId))
            {
                throw new ArgumentNullException("applicationId");
            }

            // add cert and private key to device twin as tags
            Twin twin = _IoTHubDeviceRegistry.GetTwinAsync(applicationId.Identifier.ToString()).Result;
            if (twin != null)
            {
                string patch = "{ tags: { ";

                patch += "Certificate: {";
                string cert = Convert.ToBase64String(certificate);
                int i = 0;
                for (i = 0; i < cert.Length / 512; i++)
                {
                    patch += (i.ToString() + ": " + JsonConvert.SerializeObject(cert.Substring(i * 512, 512)) + ",");
                }
                patch += (i.ToString() + ": " + JsonConvert.SerializeObject(cert.Substring(i * 512)));

                if (privateKey != null)
                {
                    patch += "},";
                    patch += "PrivateKey: {";
                    string pKey = Convert.ToBase64String(privateKey);
                    for (i = 0; i < pKey.Length / 512; i++)
                    {
                        patch += (i.ToString() + ": " + JsonConvert.SerializeObject(pKey.Substring(i * 512, 512)) + ",");
                    }
                    patch += (i.ToString() + ": " + JsonConvert.SerializeObject(pKey.Substring(i * 512)));
                    patch += "}";
                }
                else
                {
                    patch += "}";
                }

                patch += "} }";

                twin = _IoTHubDeviceRegistry.UpdateTwinAsync(twin.DeviceId, patch, twin.ETag).Result;
                if (twin == null)
                {
                    throw new Exception("IoTHub device update failed");
                }
            }
            else
            {
                throw new Exception("IoTHub twin retrieval failed");
            }

            return new NodeId(applicationId.Identifier.ToString(), NamespaceIndex);
        }

        public override void ApproveCertificateRequest(NodeId requestId, bool isRejected)
        {
#if TODO
            if (NodeId.IsNull(requestId))
            {
                throw new ArgumentNullException(nameof(requestId));
            }

            Guid? id = requestId.Identifier as Guid?;

            if (id == null)
            {
                throw new ServiceResultException(StatusCodes.BadNodeIdInvalid);
            }

            using (gdsdbEntities entities = new gdsdbEntities())
            {
                var request = (from x in entities.CertificateRequests where x.RequestId == id select x).SingleOrDefault();

                if (request == null)
                {
                    throw new ServiceResultException(StatusCodes.BadNodeIdUnknown);
                }

                request.State = (int)((isRejected)?CertificateRequestState.Rejected:CertificateRequestState.Approved);
                entities.SaveChanges();
            }
#endif
        }

        public override bool CompleteCertificateRequest(
            NodeId applicationId,
            NodeId requestId,
            out byte[] certificate,
            out byte[] privateKey)
        {
            Twin twin = _IoTHubDeviceRegistry.GetTwinAsync(applicationId.Identifier.ToString()).Result;
            if ((twin != null) && (twin.Tags.Count > 0))
            {
                try
                {
                    string cert = string.Empty;
                    JObject certParts = (JObject)twin.Tags["Certificate"];
                    foreach (JToken part in certParts.Children())
                    {
                        cert += ((JProperty)part).Value;
                    }
                    certificate = Convert.FromBase64String(cert);
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
                }
                catch (Exception)
                {
                    privateKey = null;
                }
            }
            else
            {
                throw new Exception("IoTHub twin retrieval failed");
            }
            return true;
        }

        public override void UnregisterApplication(
            NodeId applicationId,
            out byte[] certificate,
            out byte[] privateKey)
        {
            if (NodeId.IsNull(applicationId))
            {
                throw new ArgumentNullException("applicationId");
            }

            CompleteCertificateRequest(applicationId, null, out certificate, out privateKey);

            _IoTHubDeviceRegistry.RemoveDeviceAsync(applicationId.Identifier.ToString()).Wait();
        }

        public override ApplicationRecordDataType GetApplication(NodeId applicationId)
        {
            if (NodeId.IsNull(applicationId))
            {
                return null;
            }

            if (applicationId.IdType != IdType.String || NamespaceIndex != applicationId.NamespaceIndex)
            {
                return null;
            }

            Twin twin = _IoTHubDeviceRegistry.GetTwinAsync(applicationId.Identifier.ToString()).Result;
            if ((twin != null) && (twin.Tags.Count > 0))
            {
                try
                {
                    ApplicationRecordDataType record = new ApplicationRecordDataType();

                    record.ApplicationId = new NodeId(applicationId.Identifier.ToString(), NamespaceIndex);
                    record.ApplicationUri = applicationId.Identifier.ToString();
                    record.ApplicationType = (ApplicationType)twin.Tags["ApplicationType"].Value;
                    record.ProductUri = twin.Tags["ProductUri"].Value;

                    record.ApplicationNames = new LocalizedTextCollection();
                    JObject appNamesParts = (JObject)twin.Tags["ApplicationNames"];
                    foreach (JToken part in appNamesParts.Children())
                    {
                        record.ApplicationNames.Add(new LocalizedText(((JProperty)part).Value.ToString()));
                    }

                    record.ServerCapabilities = new StringCollection();
                    JObject serverCapsParts = (JObject)twin.Tags["ServerCapabilities"];
                    foreach (JToken part in serverCapsParts.Children())
                    {
                        record.ServerCapabilities.Add(((JProperty)part).Value.ToString());
                    }

                    record.DiscoveryUrls = new StringCollection();
                    JObject discoURLsParts = (JObject)twin.Tags["DiscoveryUrls"];
                    foreach (JToken part in discoURLsParts.Children())
                    {
                        record.DiscoveryUrls.Add(((JProperty)part).Value.ToString());
                    }

                    return record;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public override ApplicationRecordDataType[] FindApplications(string applicationUri)
        {
            List<ApplicationRecordDataType> records = new List<ApplicationRecordDataType>();

            ApplicationRecordDataType record = GetApplication(new NodeId(applicationUri, NamespaceIndex));
            if (record != null)
            {
                records.Add(record);
            }

            return records.ToArray();
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
            lastCounterResetTime = DateTime.MinValue;
            List<ServerOnNetwork> records = new List<ServerOnNetwork>();

            RegistryStatistics stats = _IoTHubDeviceRegistry.GetRegistryStatisticsAsync().Result;
            if (stats != null)
            {
                var list = _IoTHubDeviceRegistry.GetDevicesAsync((int)stats.TotalDeviceCount).Result;
                uint i = 0;
                foreach (Device device in list)
                {
                    ApplicationRecordDataType record = GetApplication(new NodeId(device.Id, NamespaceIndex));
                    if (record != null)
                    {
                        ServerOnNetwork server = new ServerOnNetwork();
                        server.RecordId = i++;
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
                    }
                }
            }

            return records.ToArray();
        }
    }
}
