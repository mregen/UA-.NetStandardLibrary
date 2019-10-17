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

        private static bool BrowseTypeIds(
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
            browser.NodeClassMask = (int)NodeClass.Object;

            var references = browser.Browse(nodeId);

            if (references.Count >= 1)
            {
                encodingId = references.First().NodeId;
                var encodingNodeId = ExpandedNodeId.ToNodeId(encodingId, session.NamespaceUris);
                encodingId = NodeId.ToExpandedNodeId(encodingNodeId, session.NamespaceUris);
                browser.BrowseDirection = BrowseDirection.Inverse;
                browser.ReferenceTypeId = ReferenceTypeIds.HasEncoding;
                browser.IncludeSubtypes = false;
                browser.NodeClassMask = (int)NodeClass.DataType;
                references = browser.Browse(encodingNodeId);
                if (references.Count >= 1)
                {
                    typeId = references.First().NodeId;
                    var typeNodeId = ExpandedNodeId.ToNodeId(typeId, session.NamespaceUris);
                    typeId = NodeId.ToExpandedNodeId(typeNodeId, session.NamespaceUris);

                    var node = session.ReadNode(typeNodeId);
                    var dataTypeNode = node as DataTypeNode;
                    if (dataTypeNode != null &&
                        dataTypeNode.DataTypeDefinition != null)
                    {
                        Console.WriteLine($"{dataTypeNode.DataTypeDefinition}");
                        // not supported yet
                        return false;
                    }

#if CHECKBROWSE
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
#endif
                    return true;
                }
            }

            return false;
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

            session.Browse(
                null,
                null,
                DataTypeIds.Enumeration,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HasSubtype,
                false,
                (uint)NodeClass.DataType,
                out continuationPoint,
                out references);

            ReferenceDescriptionCollection dictList = new ReferenceDescriptionCollection();
            dictList.AddRange(references.Where(rd => rd.NodeId.NamespaceIndex != 0));

            foreach (var dictEntry in dictList)
            {
                Console.WriteLine($"Dictionary Id: {dictEntry}");

                // fix expanded nodeids
                var nodeId = dictEntry.NodeId;
                dictEntry.NodeId = NodeId.ToExpandedNodeId(ExpandedNodeId.ToNodeId(nodeId, session.NamespaceUris), session.NamespaceUris);
            }

            // load binary type system
            var typeSystem = await session.LoadTypeSystem();

            foreach (var dictionaryId in typeSystem)
            {
                var dictionary = dictionaryId.Value;
                Console.WriteLine($"Dictionary: {dictionary.Name}");
                Console.WriteLine($"Namespace : {dictionary.TypeDictionary.TargetNamespace}");
                dictionary.DataTypes.Values.ToList().ForEach(i => Console.WriteLine(i.ToString()));

                // hackhack .. sort dictionary for dependencies
                var structureList = new List<Opc.Ua.Schema.Binary.TypeDescription>();
                var enumList = new List<Opc.Ua.Schema.Binary.TypeDescription>();
                var itemList = dictionary.TypeDictionary.Items.ToList();
                foreach (var item in itemList)
                {
                    var structuredObject = item as Opc.Ua.Schema.Binary.StructuredType;
                    if (structuredObject != null)
                    {
                        var dependentFields = structuredObject.Field.Where(f => f.TypeName.Namespace == dictionary.TypeDictionary.TargetNamespace);
                        if (dependentFields.Count() == 0)
                        {
                            structureList.Insert(0, structuredObject);
                        }
                        else
                        {
                            int insertIndex = 0;
                            foreach (var field in dependentFields)
                            {
                                int index = structureList.FindIndex(t => t.Name == field.Name);
                                if (index > insertIndex)
                                {
                                    insertIndex = index;
                                }
                            }
                            insertIndex++;
                            if (structureList.Count > insertIndex)
                            {
                                structureList.Insert(insertIndex, structuredObject);
                            }
                            else
                            {
                                structureList.Add(structuredObject);
                            }
                        }
                    }
                    else if (item is Opc.Ua.Schema.Binary.EnumeratedType)
                    {
                        enumList.Add(item);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }

                var complexTypeBuilder = new ComplexTypeBuilder(dictionary.TypeDictionary.TargetNamespace);

                // build enums
                foreach (var item in enumList)
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
                        var newType = complexTypeBuilder.AddEnumType(enumeratedObject);
                        // match namespace and add to type factory
                        var referenceId = dictList.Where(t =>
                            t.DisplayName == enumeratedObject.Name &&
                            t.NodeId.NamespaceUri == dictionary.TypeDictionary.TargetNamespace).FirstOrDefault();
                        if (referenceId != null)
                        {
                            session.Factory.AddEncodeableType(referenceId.NodeId, newType);
                        }
                        else
                        {
                            Console.WriteLine($"ERROR: Failed to match enum type {enumeratedObject.Name} to namespace {dictionary.TypeDictionary.TargetNamespace}.");
                        }
                    }
                }

                // build classes
                foreach (var item in structureList)
                {
                    var structuredObject = item as Opc.Ua.Schema.Binary.StructuredType;
                    if (structuredObject != null)
                    {
                        bool missingTypeInfo = false;
                        var nodeId = dictionary.DataTypes.Where(d => d.Value.DisplayName == item.Name).FirstOrDefault().Value;
                        ExpandedNodeId typeId;
                        ExpandedNodeId binaryEncodingId;

                        Console.WriteLine($"§§§§§§§ {nameof(item.Name)}: {item.Name} §§§§§§§");
                        var structureBuilder = complexTypeBuilder.AddStructuredType(item.Name);
                        int order = 10;
                        bool unsupportedTypeInfo = false;
                        foreach (var field in structuredObject.Field)
                        {
                            // check for yet unsupported properties
                            Console.WriteLine($"{nameof(field.Name)}: {field.Name}");
                            Console.WriteLine($"{nameof(field.TypeName)}: {field.TypeName}");
                            Console.WriteLine($"{nameof(field.Length)}: {field.Length}");
                            Console.WriteLine($"{nameof(field.LengthField)}: {field.LengthField}");
                            Console.WriteLine($"{nameof(field.IsLengthInBytes)}: {field.IsLengthInBytes}");
                            Console.WriteLine($"{nameof(field.SwitchField)}: {field.SwitchField}");
                            Console.WriteLine($"{nameof(field.SwitchValue)}: {field.SwitchValue}");
                            Console.WriteLine($"{nameof(field.SwitchOperand)}: {field.SwitchOperand}");
                            Console.WriteLine($"{nameof(field.Terminator)}: {field.Terminator}");

                            if (field.IsLengthInBytes ||
                                field.SwitchField != null ||
                                field.Terminator != null ||
                                field.LengthField != null ||
                                field.Length != 0)
                            {
                                Console.WriteLine("---------- unsupported type --------------");
                                unsupportedTypeInfo = true;
                            }

                            if (unsupportedTypeInfo)
                            {
                                continue;
                            }

                            Type fieldType = null;
                            if (field.TypeName.Namespace == Namespaces.OpcBinarySchema ||
                                field.TypeName.Namespace == Namespaces.OpcUa)
                            {
                                if (field.TypeName.Name == "Bit")
                                {
                                    Console.WriteLine("---------- unsupported type --------------");
                                    unsupportedTypeInfo = true;
                                    continue;
                                }
                                // check for built in type
                                if (field.TypeName.Name == "CharArray")
                                {
                                    field.TypeName = new System.Xml.XmlQualifiedName("ByteString", field.TypeName.Namespace);
                                }
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
                                Console.WriteLine("---------- no type information --------------");
                                continue;
                            }
                            Console.WriteLine($"Add: {field.Name}:{fieldType}-{order}");
                            structureBuilder.AddField(field.Name, fieldType, order);
                            order += 10;
                        }

                        bool newTypeDescription = BrowseTypeIds(session, ExpandedNodeId.ToNodeId(nodeId.NodeId, session.NamespaceUris),
                            out typeId, out binaryEncodingId);

                        if (!newTypeDescription)
                        {
                            Console.WriteLine($"--------- {item.Name}-Type id not found");
                        }

                        if (!missingTypeInfo && !unsupportedTypeInfo)
                        {
                            var complexType = structureBuilder.CreateType();
                            Console.WriteLine($"§§§§§§§§§§§§ {item.Name}-{complexType} §§§§§§§§§§§§");
                            session.Factory.AddEncodeableType(binaryEncodingId, complexType);
                            session.Factory.AddEncodeableType(typeId, complexType);
                        }
                    }
                }
            }

            // UA Ansi C server
            if (TestNodeId(new NodeId("Demo.WorkOrder.WorkOrderVariable2.StatusComments", 4)))
            {
                // WorkOrderStatusType
                var statusNodeId = new NodeId("Demo.WorkOrder.WorkOrderVariable2.StatusComments", 4);
                var statusCommentNodeId = session.ReadNode(statusNodeId);
                var statusComment = session.ReadValue(statusNodeId);

                // Vector
                var nodeId = new NodeId("Demo.Static.Scalar.Vector", 4);
                var vector = session.ReadNode(nodeId);
                var vectorValue = session.ReadValue(nodeId);

                nodeId = new NodeId("Demo.Static.Arrays.Vector", 4);
                var vectorArray = session.ReadNode(nodeId);
                var vectorArrayValue = session.ReadValue(nodeId);

                nodeId = new NodeId("Demo.Static.Matrix.Vector", 4);
                var vectorMatrix = session.ReadNode(nodeId);
                var vectorMatrixValue = session.ReadValue(nodeId);

                // AccessRights
                nodeId = new NodeId("Demo.Static.Scalar.OptionSet", 4);
                var optionSet = session.ReadNode(nodeId);
                var optionSetValue = session.ReadValue(nodeId);

                // structure
                nodeId = new NodeId("Demo.Static.Scalar.Structure", 4);
                var node = session.ReadNode(nodeId);
                var value = session.ReadValue(nodeId);
            }

            // Quickstart DataTypes server
            if (TestNodeId(new NodeId(283, 4)))
            {
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
            }

            Console.WriteLine("4 - Browse the OPC UA server namespace.");
            exitCode = ExitCode.ErrorBrowseNamespace;


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

            Console.WriteLine("8 - Running...Press Ctrl-C to exit...");
            exitCode = ExitCode.ErrorRunning;
        }

        private bool TestNodeId(NodeId nodeId)
        {
            try
            {
                session.ReadNode(nodeId);
                return true;
            }
            catch
            {
            }
            return false;
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
