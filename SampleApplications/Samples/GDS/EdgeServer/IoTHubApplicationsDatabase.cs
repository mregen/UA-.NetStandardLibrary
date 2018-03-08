using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Opc.Ua.Gds.Server.Database
{

    class ByteArrayConverter : JsonConverter
    {
        const int TwinTagsMaxBlobSize = 512;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(byte[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            byte[] blob = (byte[])value;
            string base64 = Convert.ToBase64String(blob);
            int i = 0;
            writer.WriteStartObject();
            for (i = 0; i < base64.Length / TwinTagsMaxBlobSize; i++)
            {
                writer.WritePropertyName(i.ToString());
                writer.WriteValue(base64.Substring(i * TwinTagsMaxBlobSize, TwinTagsMaxBlobSize));
            }
            writer.WritePropertyName(i.ToString());
            writer.WriteValue(base64.Substring(i * TwinTagsMaxBlobSize));
            writer.WriteEndObject();
        }
    }

    class StringArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string[] blob = (string[])value;
            int i = 0;
            writer.WriteStartObject();
            for (i = 0; i < blob.Length; i++)
            {
                writer.WritePropertyName(i.ToString());
                writer.WriteValue(blob[i]);
            }
            writer.WriteEndObject();
        }
    }

    [Serializable]
    class ApplicationTwinRecord
    {
        //public uint? ID { get; set; }
        public Guid ApplicationId { get; set; }
        public string ApplicationUri { get; set; }
        [JsonConverter(typeof(StringArrayConverter))]
        public string[] ApplicationNames { get; set; }
        public int ApplicationType { get; set; }
        public string ProductUri { get; set; }
        [JsonConverter(typeof(StringArrayConverter))]
        public string[] DiscoveryUrls { get; set; }
        public string ServerCapabilities { get; set; }
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] Certificate { get; set; }
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] HttpsCertificate { get; set; }
        public Guid? TrustListId { get; set; }
        public Guid? HttpsTrustListId { get; set; }
    }
    [Serializable]
    class CertificateRequestTwinRecord
    {
        public Guid RequestId { get; set; }
        public int? State { get; set; }
        [JsonConverter(typeof(ByteArrayConverter)), JsonRequired]
        public byte[] Certificate { get; set; }
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] PrivateKey { get; set; }
        public string AuthorityId { get; set; }
    }

    public class IoTHubApplicationsDatabase : ApplicationsDatabaseBase
    {
        RegistryManager _IoTHubDeviceRegistry = null;

        public IoTHubApplicationsDatabase(string databaseStorePath, bool clean = false)
        {
            _IoTHubDeviceRegistry = RegistryManager.CreateFromConnectionString(databaseStorePath);

            if (clean)
            {
                var query = _IoTHubDeviceRegistry.CreateQuery("SELECT * FROM devices", 10);
                while (query.HasMoreResults)
                {
                    var page = query.GetNextAsTwinAsync().Result;
                    foreach (var twin in page)
                    {
                        Utils.Trace("Remove Device:" + twin.DeviceId);
                        _IoTHubDeviceRegistry.RemoveDeviceAsync(twin.DeviceId).Wait();
                    }
                }
            }
        }
        public override NodeId RegisterApplication(
            ApplicationRecordDataType application
            )
        {
            NodeId appNodeId = base.RegisterApplication(application);
            Guid applicationId = GetNodeIdGuid(appNodeId);
            string capabilities = base.ServerCapabilities(application);

            Device device = null;

            if (applicationId != Guid.Empty)
            {
                device = _IoTHubDeviceRegistry.GetDeviceAsync(applicationId.ToString()).Result;
            }

            if (device == null)
            {
                applicationId = Guid.NewGuid();
                device = _IoTHubDeviceRegistry.AddDeviceAsync(new Device(applicationId.ToString())).Result;

                if (device == null)
                {
                    throw new Exception("IoTHub register application failed.");
                }
            }

            Twin twin = _IoTHubDeviceRegistry.GetTwinAsync(applicationId.ToString()).Result;
            if (twin != null)
            {
                ApplicationTwinRecord record = new ApplicationTwinRecord() { ApplicationId = applicationId };
                record.ApplicationUri = application.ApplicationUri;
                List<string> applicationNames = new List<string>();
                foreach (var name in application.ApplicationNames)
                {
                    applicationNames.Add(name.Text);
                }
                record.ApplicationNames = applicationNames.ToArray();
                record.ApplicationType = (int)application.ApplicationType;
                record.ProductUri = application.ProductUri;
                record.ServerCapabilities = capabilities;
                record.DiscoveryUrls = application.DiscoveryUrls.ToArray();

                string twinPatch = TwinPatchFromRecord(record);
                twin = _IoTHubDeviceRegistry.UpdateTwinAsync(twin.DeviceId, twinPatch, twin.ETag).Result;
                if (twin == null)
                {
                    throw new Exception("IoTHub registration update failed.");
                }
            }
            else
            {
                throw new Exception("IoTHub twin retrieval failed");
            }

            return new NodeId(applicationId, NamespaceIndex);
        }

        public override NodeId CreateCertificateRequest(
            NodeId applicationId,
            byte[] certificate,
            byte[] privateKey,
            string authorityId)
        {
            Guid id = GetNodeIdGuid(applicationId);

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
        }

        public override void ApproveCertificateRequest(NodeId requestId, bool isRejected)
        {
            Guid id = GetNodeIdGuid(requestId);
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
            Guid appId = GetNodeIdGuid(applicationId);

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
            return true;
        }

        public override void UnregisterApplication(
            NodeId applicationId,
            out byte[] certificate,
            out byte[] httpsCertificate)
        {
            certificate = null;
            httpsCertificate = null;

            Guid id = GetNodeIdGuid(applicationId);

            List<byte[]> certificates = new List<byte[]>();

            Twin twin = _IoTHubDeviceRegistry.GetTwinAsync(id.ToString()).Result;
            if ((twin != null) && (twin.Tags.Count > 0))
            {
                // todo: return certs
            }

            _IoTHubDeviceRegistry.RemoveDeviceAsync(id.ToString()).Wait();
        }

        public override ApplicationRecordDataType GetApplication(NodeId applicationId)
        {
            Guid id = GetNodeIdGuid(applicationId);

            Twin twin = _IoTHubDeviceRegistry.GetTwinAsync(id.ToString()).Result;
            if ((twin != null) && (twin.Tags.Count > 0))
            {
                try
                {
                    ApplicationRecordDataType record = new ApplicationRecordDataType();

                    record.ApplicationId = applicationId;
                    record.ApplicationUri = twin.Tags["ApplicationUri"].Value;
                    record.ApplicationType = (ApplicationType)twin.Tags["ApplicationType"].Value;
                    record.ProductUri = twin.Tags["ProductUri"].Value;

                    record.ApplicationNames = new LocalizedTextCollection();
                    try
                    {
                        JObject appNamesParts = (JObject)twin.Tags["ApplicationNames"];
                        foreach (JToken part in appNamesParts.Children())
                        {
                            record.ApplicationNames.Add(new LocalizedText(((JProperty)part).Value.ToString()));
                        }
                    }
                    catch { }


                    record.ServerCapabilities = new StringCollection();
                    string serverCapabilities = twin.Tags["ServerCapabilities"];
                    if (!String.IsNullOrWhiteSpace(serverCapabilities))
                    {
                        record.ServerCapabilities.AddRange(serverCapabilities.Split(','));
                    }

                    record.DiscoveryUrls = new StringCollection();
                    try
                    {
                        JObject discoURLsParts = (JObject)twin.Tags["DiscoveryUrls"];
                        foreach (JToken part in discoURLsParts.Children())
                        {
                            record.DiscoveryUrls.Add(((JProperty)part).Value.ToString());
                        }
                    }
                    catch { }
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
            try
            {
                string requestIdQuery = String.Format("SELECT * FROM devices WHERE tags.ApplicationUri = '{0}'", applicationUri);
                var query = _IoTHubDeviceRegistry.CreateQuery(requestIdQuery, 10);
                while (query.HasMoreResults)
                {
                    var page = query.GetNextAsTwinAsync().Result;
                    foreach (var twin in page)
                    {
                        try
                        {
                            Guid appGuid = new Guid(twin.Tags["ApplicationId"].Value);
                            ApplicationRecordDataType record = GetApplication(new NodeId(appGuid, NamespaceIndex));
                            if (record != null)
                            {
                                records.Add(record);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch
            {

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
                // TODO use CreateQuery("select * from devices", (int)stats.TotalDeviceCount);
                var list = _IoTHubDeviceRegistry.GetDevicesAsync((int)stats.TotalDeviceCount).Result;
                uint i = 0;
                foreach (Device device in list)
                {
                    if (Guid.TryParse(device.Id, out Guid deviceIdGuid))
                    {
                        ApplicationRecordDataType record = GetApplication(new NodeId(deviceIdGuid, NamespaceIndex));
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
            }

            return records.ToArray();
        }

        private string TwinPatchFromRecord(ApplicationTwinRecord record)
        {
            string twinPatch = "{ tags: " +
                JsonConvert.SerializeObject(
                    record,
                    Newtonsoft.Json.Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }) +
                "}";
            return twinPatch;
        }

        private string TwinPatchFromRecord(CertificateRequestTwinRecord record)
        {
            string twinPatch = "{ tags: " +
                JsonConvert.SerializeObject(
                    record,
                    Newtonsoft.Json.Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }) +
                "}";
            return twinPatch;
        }
    }
}
