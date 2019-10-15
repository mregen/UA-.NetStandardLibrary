/* Copyright (c) 1996-2019 The OPC Foundation. All rights reserved.
   The source code in this file is covered under a dual-license scenario:
     - RCL: for OPC Foundation members in good-standing
     - GPL V2: everybody else
   RCL license terms accompanied with this source code. See http://opcfoundation.org/License/RCL/1.00/
   GNU General Public License as published by the Free Software Foundation;
   version 2 of the License are accompanied with this source code. See http://opcfoundation.org/License/GPLv2
   This source code is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
*/

using Mono.Options;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace NetCoreConsoleClient
{

    public enum ExitCode : int
    {
        Ok = 0,
        ErrorCreateApplication = 0x11,
        ErrorDiscoverEndpoints = 0x12,
        ErrorCreateSession = 0x13,
        ErrorBrowseNamespace = 0x14,
        ErrorCreateSubscription = 0x15,
        ErrorMonitoredItem = 0x16,
        ErrorAddSubscription = 0x17,
        ErrorRunning = 0x18,
        ErrorNoKeepAlive = 0x30,
        ErrorInvalidCommandLine = 0x100
    };

    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine(
                (Utils.IsRunningOnMono() ? "Mono" : ".Net Core") +
                " OPC UA Console Client sample");

            // command line options
            bool showHelp = false;
            int stopTimeout = Timeout.Infinite;
            bool autoAccept = false;

            Mono.Options.OptionSet options = new Mono.Options.OptionSet {
                { "h|help", "show this message and exit", h => showHelp = h != null },
                { "a|autoaccept", "auto accept certificates (for testing only)", a => autoAccept = a != null },
                { "t|timeout=", "the number of seconds until the client stops.", (int t) => stopTimeout = t }
            };

            IList<string> extraArgs = null;
            try
            {
                extraArgs = options.Parse(args);
                if (extraArgs.Count > 1)
                {
                    foreach (string extraArg in extraArgs)
                    {
                        Console.WriteLine("Error: Unknown option: {0}", extraArg);
                        showHelp = true;
                    }
                }
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                showHelp = true;
            }

            if (showHelp)
            {
                // show some app description message
                Console.WriteLine(Utils.IsRunningOnMono() ?
                    "Usage: mono MonoConsoleClient.exe [OPTIONS] [ENDPOINTURL]" :
                    "Usage: dotnet NetCoreConsoleClient.dll [OPTIONS] [ENDPOINTURL]");
                Console.WriteLine();

                // output the options
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return (int)ExitCode.ErrorInvalidCommandLine;
            }

            string endpointURL;
            if (extraArgs.Count == 0)
            {
                // use OPC UA .Net Sample server 
                endpointURL = "opc.tcp://localhost:51210/UA/SampleServer";
            }
            else
            {
                endpointURL = extraArgs[0];
            }

            MySampleClient client = new MySampleClient(endpointURL, autoAccept, stopTimeout);
            client.Run();

            return (int)MySampleClient.ExitCode;
        }
    }

    public class MySampleClient
    {
        const int ReconnectPeriod = 10;
        Session session;
        SessionReconnectHandler reconnectHandler;
        string endpointURL;
        int clientRunTime = Timeout.Infinite;
        static bool autoAccept = false;
        static ExitCode exitCode;

        public MySampleClient(string _endpointURL, bool _autoAccept, int _stopTimeout)
        {
            endpointURL = _endpointURL;
            autoAccept = _autoAccept;
            clientRunTime = _stopTimeout <= 0 ? Timeout.Infinite : _stopTimeout * 1000;
        }

        public void Run()
        {
            try
            {
                ConsoleSampleClient().Wait();
            }
            catch (Exception ex)
            {
                Utils.Trace("ServiceResultException:" + ex.Message);
                Console.WriteLine("Exception: {0}", ex.Message);
                return;
            }

            ManualResetEvent quitEvent = new ManualResetEvent(false);
            try
            {
                Console.CancelKeyPress += (sender, eArgs) =>
                {
                    quitEvent.Set();
                    eArgs.Cancel = true;
                };
            }
            catch
            {
            }

            // wait for timeout or Ctrl-C
            quitEvent.WaitOne(clientRunTime);

            // return error conditions
            if (session.KeepAliveStopped)
            {
                exitCode = ExitCode.ErrorNoKeepAlive;
                return;
            }

            exitCode = ExitCode.Ok;
        }

        public static ExitCode ExitCode => exitCode;

        private static void BrowseTypeIds(
            Session session,
            NodeId nodeId,
            out ExpandedNodeId typeId,
            out ExpandedNodeId encodingId)
        {
            typeId = ExpandedNodeId.Null;
            encodingId = ExpandedNodeId.Null;

            Browser browser = new Browser(session);

            browser.BrowseDirection = BrowseDirection.Inverse;
            browser.ReferenceTypeId = ReferenceTypeIds.HasDescription;
            browser.IncludeSubtypes = false;
            browser.NodeClassMask = 0;

            ReferenceDescriptionCollection references = browser.Browse(nodeId);

            if (references.Count == 1)
            {
                encodingId = references.First().NodeId;
                var encodingNodeId = ExpandedNodeId.ToNodeId(encodingId, session.NamespaceUris);
                encodingId = NodeId.ToExpandedNodeId(encodingNodeId, session.NamespaceUris);
                browser.BrowseDirection = BrowseDirection.Inverse;
                browser.ReferenceTypeId = ReferenceTypeIds.HasEncoding;
                browser.IncludeSubtypes = false;
                browser.NodeClassMask = 0;
                references = browser.Browse(encodingNodeId);
                if (references.Count == 1)
                {
                    typeId = references.First().NodeId;
                    var typeNodeId = ExpandedNodeId.ToNodeId(typeId, session.NamespaceUris);
                    typeId = NodeId.ToExpandedNodeId(typeNodeId, session.NamespaceUris);
                    browser.BrowseDirection = BrowseDirection.Forward;
                    browser.ReferenceTypeId = ReferenceTypeIds.HasEncoding;
                    browser.IncludeSubtypes = false;
                    browser.NodeClassMask = 0;
                    references = browser.Browse(typeNodeId);
                    if (references.Count > 0)
                    {
                        foreach (var reference in references)
                        {
                        }
                    }
                    return;
                }
            }

            throw new Exception();
        }

        private async Task ConsoleSampleClient()
        {
            Console.WriteLine("1 - Create an Application Configuration.");
            exitCode = ExitCode.ErrorCreateApplication;

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "UA Core Sample Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = Utils.IsRunningOnMono() ? "Opc.Ua.MonoSampleClient" : "Opc.Ua.SampleClient"
            };

            // load the application configuration.
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);

            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (haveAppCertificate)
            {
                config.ApplicationUri = Utils.GetApplicationUriFromCertificate(config.SecurityConfiguration.ApplicationCertificate.Certificate);
                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    autoAccept = true;
                }
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }
            else
            {
                Console.WriteLine("    WARN: missing application certificate, using unsecure connection.");
            }

            Console.WriteLine("2 - Discover endpoints of {0}.", endpointURL);
            exitCode = ExitCode.ErrorDiscoverEndpoints;
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, haveAppCertificate, 15000);
            Console.WriteLine("    Selected endpoint uses: {0}",
                selectedEndpoint.SecurityPolicyUri.Substring(selectedEndpoint.SecurityPolicyUri.LastIndexOf('#') + 1));

            Console.WriteLine("3 - Create a session with OPC UA server.");
            exitCode = ExitCode.ErrorCreateSession;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            session = await Session.Create(config, endpoint, false, "OPC UA Console Client", 60000, new UserIdentity(new AnonymousIdentityToken()), null);

            // register keep alive handler
            session.KeepAlive += Client_KeepAlive;

            Console.WriteLine("4 - Browse the OPC UA data dictionary.");
            exitCode = ExitCode.ErrorBrowseNamespace;
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;

            references = session.FetchReferences(ObjectIds.TypesFolder);

            var rootList = new List<NodeId>();
            var dictList = new ReferenceDescriptionCollection();
            //rootList.Add(ObjectIds.TypesFolder);
            //rootList.Add(DataTypeIds.BaseDataType);
            rootList.Add(DataTypeIds.Structure);
            rootList.Add(DataTypeIds.Enumeration);
            //browseList.Add(ReferenceTypeIds.HierarchicalReferences);
            //browseList.Add(ReferenceTypeIds.Organizes);
            //browseList.Add(ReferenceTypeIds.Aggregates);

            var types = /*(uint)NodeClass.VariableType | (uint)NodeClass.ObjectType |
                (uint)NodeClass.ReferenceType |*/ (uint)NodeClass.DataType /*|
                (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method*/;
            types = 0;

            do
            {
                var nextRootList = new List<NodeId>();
                foreach (var root in rootList)
                {
                    session.Browse(
                        null,
                        null,
                        root,
                        0u,
                        BrowseDirection.Forward,
                        ReferenceTypeIds.HierarchicalReferences,
                        true,
                        types,
                        out continuationPoint,
                        out references);

                    if (references.Count > 0)
                    {
                        Console.WriteLine($"Browse -- {root} has {references.Count} references");
                    }

                    foreach (var rd in references)
                    {
                        if (rd.NodeId.NamespaceIndex == 0)
                        {
                            // skip well known NodeIds
                            continue;
                        }

                        Console.WriteLine(" {0}, {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);

                        if (rd.NodeClass == NodeClass.DataType)
                        {
                            NodeId nodeId = ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris);
                            dictList.Add(rd);
                            nextRootList.Add(nodeId);
                        }

                        continue;

                        ReferenceDescriptionCollection nextRefs;
                        byte[] nextCp;
                        session.Browse(
                            null,
                            null,
                            ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris),
                            0u,
                            BrowseDirection.Forward,
                            ReferenceTypeIds.HierarchicalReferences,
                            true,
                            types,
                            out nextCp,
                            out nextRefs);

                        foreach (var nextRd in nextRefs)
                        {
                            Console.WriteLine("   + {0}, {1}, {2}", nextRd.DisplayName, nextRd.BrowseName, nextRd.NodeClass);
                            if (nextRd.NodeClass == NodeClass.DataType)
                            {
                                dictList.Add(nextRd);
                            }
                        }
                    }
                }
                rootList = nextRootList;
            } while (rootList.Count > 0);

            //var resultVehicleType = await session.FindDataDictionary(new NodeId(353, 4));
            //var resultTwoWheelerX = await session.FindDataDictionary(new NodeId(302, 3));

            foreach (var dictEntry in dictList)
            {
                var target = new BrowsePathTarget();
                //session.FetchReferences()
                //var dict = await session.FindDataDictionary(dictEntry.BinaryEncodingId);
                Console.WriteLine($"Dictionary Id: {dictEntry}");

                // fix expanded nodeids
                var nodeId = dictEntry.NodeId;
                dictEntry.NodeId = NodeId.ToExpandedNodeId(ExpandedNodeId.ToNodeId(nodeId, session.NamespaceUris), session.NamespaceUris);
            }

            // read all type dictionaries as binary
            references = session.FetchReferences(ObjectIds.OPCBinarySchema_TypeSystem);
            foreach (var r in references)
            {
                if (r.NodeId.NamespaceIndex != 0)
                {
                    Console.WriteLine($"Read Dictionary: {r.BrowseName}");

                    DataDictionary dictionaryToLoad = new DataDictionary(session);

                    await dictionaryToLoad.Load(r);

                    dictionaryToLoad.DataTypes.Values.ToList().ForEach(i => Console.WriteLine(i.ToString()));

                    var complexTypeBuilder = new ComplexTypeBuilder();

                    Console.WriteLine($"{dictionaryToLoad.TypeDictionary.TargetNamespace}");
                    foreach (var item in dictionaryToLoad.TypeDictionary.Items)
                    {
                        if (item.QName != null)
                        {
                            Console.WriteLine($"{item.QName.Namespace}:{item.QName.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"{item.Name}");
                        }

                        var enumeratedObject = item as Opc.Ua.Schema.Binary.EnumeratedType;
                        if (enumeratedObject != null)
                        {
                            // add enum type to module
                            var newType = complexTypeBuilder.AddEnumType(enumeratedObject, dictionaryToLoad.TypeDictionary.TargetNamespace);
                            // match namespace and add to type factory
                            var referenceId = dictList.Where(t =>
                                t.DisplayName == enumeratedObject.Name &&
                                t.NodeId.NamespaceUri == dictionaryToLoad.TypeDictionary.TargetNamespace).FirstOrDefault();
                            if (referenceId != null)
                            {
                                session.Factory.AddEncodeableType(referenceId.NodeId, newType);
                            }
                            else
                            {
                                Console.WriteLine($"ERROR: Failed to match enum type {enumeratedObject.Name} to namespace {dictionaryToLoad.TypeDictionary.TargetNamespace}.");
                            }
                        }
                    }

                    foreach (var item in dictionaryToLoad.TypeDictionary.Items)
                    {
                        var structuredObject = item as Opc.Ua.Schema.Binary.StructuredType;
                        if (structuredObject != null)
                        {
                            bool missingTypeInfo = false;
                            var nodeId = dictionaryToLoad.DataTypes.Where(d => d.Value.DisplayName == item.Name).FirstOrDefault().Value;
                            ExpandedNodeId typeId;
                            ExpandedNodeId binaryEncodingId;
                            BrowseTypeIds(session, ExpandedNodeId.ToNodeId(nodeId.NodeId, session.NamespaceUris),
                                out typeId, out binaryEncodingId);

                            var structureBuilder = complexTypeBuilder.AddStructuredType(item.Name, dictionaryToLoad.TypeDictionary.TargetNamespace);
                            int order = 10;
                            foreach (var field in structuredObject.Field)
                            {
                                Type fieldType = null;
                                if (field.TypeName.Namespace == Namespaces.OpcBinarySchema)
                                {
                                    // check for built in type
                                    var internalField = typeof(DataTypeIds).GetField(field.TypeName.Name);
                                    var internalNodeId = (NodeId)internalField.GetValue(field.TypeName.Name);
                                    var builtInType = Opc.Ua.TypeInfo.GetBuiltInType(internalNodeId);
                                    fieldType = Opc.Ua.TypeInfo.GetSystemType(internalNodeId, session.Factory);
                                }
                                else
                                {
                                    var referenceId = dictList.Where(t =>
                                        t.DisplayName == field.TypeName.Name &&
                                        t.NodeId.NamespaceUri == field.TypeName.Namespace).FirstOrDefault();
                                    if (referenceId != null)
                                    {
                                        //ExpandedNodeId absoluteId = NodeId.ToExpandedNodeId(referenceId.NodeId, session.NamespaceUris);
                                        fieldType = session.Factory.GetSystemType(referenceId.NodeId);
                                    }
                                }
                                if (fieldType == null)
                                {
                                    // skip structured type ... missing datatype
                                    missingTypeInfo = true;
                                    break;
                                }
                                structureBuilder.AddField(field.Name, fieldType, order);
                                order += 10;
                            }

                            if (!missingTypeInfo)
                            {
                                var complexType = structureBuilder.CreateType();
                                session.Factory.AddEncodeableType(binaryEncodingId, complexType);
                                session.Factory.AddEncodeableType(typeId, complexType);
                            }
                        }
                    }
                }
            }

            // read all type dictionaries as xml
            references = session.FetchReferences(ObjectIds.XmlSchema_TypeSystem);
            foreach (var r in references)
            {
                if (r.NodeId.NamespaceIndex != 0)
                {
                    DataDictionary dictionaryToLoad = new DataDictionary(session);

                    await dictionaryToLoad.Load(r);

                    dictionaryToLoad.DataTypes.Values.ToList().ForEach(i => Console.WriteLine(i.ToString()));
                }

            }

#if NETSTANDARDSERVER
            // read the dictinonary which contains 'VehicleType'
            var resultTruckType = await session.FindDataDictionary(new NodeId(332, 3));
            // read the dictinonary which contains 'TwoWheelerType'
            var resultTwoWheeler = await session.FindDataDictionary(new NodeId(15018, 4));
            var schema = resultTruckType.GetSchema((NodeId)null);
            foreach (var theType in resultTruckType.DataTypes)
            {
                var truckSchema = resultTruckType.GetSchema(theType.Key);
            }
#endif


//#if QUICKSTARTSAMPLE
            // read various nodes...
            var vehiclesInLotNode = session.ReadNode(new NodeId(283, 4));
            var parkingLotNode = session.ReadNode(new NodeId(281, 4));

            //var vehiclesInLot = session.ReadValue(new NodeId(283, 4));
            var lotTypeNodeId = session.ReadNode(new NodeId(380, 4));
            var lotType = session.ReadValue(new NodeId(380, 4));
            var ownedVehiclesNodeId = session.ReadNode(new NodeId(377, 4));
            var ownedVehicles = session.ReadValue(new NodeId(377, 4));
            Console.WriteLine(ownedVehicles);
            var primaryVehicleNode = session.ReadNode(new NodeId(376, 4));
            var primaryVehicle = session.ReadValue(new NodeId(376, 4));
            Console.WriteLine(primaryVehicle);
            var vehiclesInLot = session.ReadValue(new NodeId(283, 4));
            Console.WriteLine(vehiclesInLot);

            Console.WriteLine("4 - Browse the OPC UA server namespace.");
            exitCode = ExitCode.ErrorBrowseNamespace;
//#endif
            references = session.FetchReferences(ObjectIds.ObjectsFolder);

            session.Browse(
                null,
                null,
                ObjectIds.ObjectsFolder,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                out continuationPoint,
                out references);

            Console.WriteLine(" DisplayName, BrowseName, NodeClass");
            foreach (var rd in references)
            {
                Console.WriteLine(" {0}, {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);
                ReferenceDescriptionCollection nextRefs;
                byte[] nextCp;
                session.Browse(
                                    null,
                                    null,
                                    ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris),
                                    0u,
                                    BrowseDirection.Forward,
                                    ReferenceTypeIds.HierarchicalReferences,
                                    true,
                                    (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                                    out nextCp,
                                    out nextRefs);

                foreach (var nextRd in nextRefs)
                {
                    Console.WriteLine("   + {0}, {1}, {2}", nextRd.DisplayName, nextRd.BrowseName, nextRd.NodeClass);
                }
            }

#if mist
            Console.WriteLine("5 - Create a subscription with publishing interval of 1 second.");
            exitCode = ExitCode.ErrorCreateSubscription;
            var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000 };

            Console.WriteLine("6 - Add a list of items (server current time and status) to the subscription.");
            exitCode = ExitCode.ErrorMonitoredItem;
            var list = new List<MonitoredItem> {
                new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = "ServerStatusCurrentTime", StartNodeId = "i="+Variables.Server_ServerStatus_CurrentTime.ToString()
                }
            };
            list.ForEach(i => i.Notification += OnNotification);
            subscription.AddItems(list);

            Console.WriteLine("7 - Add the subscription to the session.");
            exitCode = ExitCode.ErrorAddSubscription;
            session.AddSubscription(subscription);
            subscription.Create();
#endif
            Console.WriteLine("8 - Running...Press Ctrl-C to exit...");
            exitCode = ExitCode.ErrorRunning;
        }

        private void Client_KeepAlive(Session sender, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                Console.WriteLine("{0} {1}/{2}", e.Status, sender.OutstandingRequestCount, sender.DefunctRequestCount);

                if (reconnectHandler == null)
                {
                    Console.WriteLine("--- RECONNECTING ---");
                    reconnectHandler = new SessionReconnectHandler();
                    reconnectHandler.BeginReconnect(sender, ReconnectPeriod * 1000, Client_ReconnectComplete);
                }
            }
        }

        private void Client_ReconnectComplete(object sender, EventArgs e)
        {
            // ignore callbacks from discarded objects.
            if (!Object.ReferenceEquals(sender, reconnectHandler))
            {
                return;
            }

            session = reconnectHandler.Session;
            reconnectHandler.Dispose();
            reconnectHandler = null;

            Console.WriteLine("--- RECONNECTED ---");
        }

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
            }
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = autoAccept;
                if (autoAccept)
                {
                    Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }

    }

}
