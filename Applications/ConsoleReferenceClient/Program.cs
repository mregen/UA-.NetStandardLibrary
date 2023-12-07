/* ========================================================================
 * Copyright (c) 2005-2021 The OPC Foundation, Inc. All rights reserved.
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua.Export;

namespace Quickstarts.ConsoleReferenceClient
{
    /// <summary>
    /// The program.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static async Task Main(string[] args)
        {
            TextWriter output = Console.Out;
            output.WriteLine("OPC UA Console Reference Client");

            output.WriteLine("OPC UA library: {0} @ {1} -- {2}",
                Utils.GetAssemblyBuildNumber(),
                Utils.GetAssemblyTimestamp().ToString("G", CultureInfo.InvariantCulture),
                Utils.GetAssemblySoftwareVersion());

            // The application name and config file names
            var applicationName = "ConsoleReferenceClient";
            var configSectionName = "Quickstarts.ReferenceClient";
            var usage = $"Usage: dotnet {applicationName}.dll [OPTIONS]";

            // command line options
            bool showHelp = false;
            bool autoAccept = false;
            string username = null;
            string userpassword = null;
            bool logConsole = false;
            bool appLog = false;
            bool renewCertificate = false;
            bool loadTypes = false;
            bool browseall = false;
            bool fetchall = false;
            bool jsonvalues = false;
            bool verbose = false;
            bool noSecurity = false;
            bool assets = false;
            bool subscribe = false;
            string password = null;
            int timeout = Timeout.Infinite;
            string logFile = null;
            string reverseConnectUrlString = null;

            Mono.Options.OptionSet options = new Mono.Options.OptionSet {
                usage,
                { "h|help", "show this message and exit", h => showHelp = h != null },
                { "a|autoaccept", "auto accept certificates (for testing only)", a => autoAccept = a != null },
                { "nsec|nosecurity", "select endpoint with security NONE, least secure if unavailable", s => noSecurity = s != null },
                { "un|username=", "the name of the user identity for the connection", (string u) => username = u },
                { "up|userpassword=", "the password of the user identity for the connection", (string u) => userpassword = u },
                { "c|console", "log to console", c => logConsole = c != null },
                { "l|log", "log app output", c => appLog = c != null },
                { "p|password=", "optional password for private key", (string p) => password = p },
                { "r|renew", "renew application certificate", r => renewCertificate = r != null },
                { "t|timeout=", "timeout in seconds to exit application", (int t) => timeout = t * 1000 },
                { "logfile=", "custom file name for log output", l => { if (l != null) { logFile = l; } } },
                { "lt|loadtypes", "Load custom types", lt => { if (lt != null) loadTypes = true; } },
                { "as|assets", "Detect Asset information", a => { if (a != null) assets = true; } },
                { "b|browseall", "Browse all references", b => { if (b != null) browseall = true; } },
                { "f|fetchall", "Fetch all nodes", f => { if (f != null) fetchall = true; } },
                { "j|json", "Output all Values as JSON", j => { if (j != null) jsonvalues = true; } },
                { "v|verbose", "Verbose output", v => { if (v != null) verbose = true; } },
                { "s|subscribe", "Subscribe", s => { if (s != null) subscribe = true; } },
                { "rc|reverseconnect=", "Connect using the reverse connect endpoint. (e.g. rc=opc.tcp://localhost:65300)", (string url) => reverseConnectUrlString = url},
            };

            ReverseConnectManager reverseConnectManager = null;

            try
            {
                // parse command line and set options
                var extraArg = ConsoleUtils.ProcessCommandLine(output, args, options, ref showHelp, "REFCLIENT", false);

                // connect Url?
                Uri serverUrl = new Uri("opc.tcp://localhost:62541/Quickstarts/ReferenceServer");
                if (!string.IsNullOrEmpty(extraArg))
                {
                    serverUrl = new Uri(extraArg);
                }

                // log console output to logger
                if (logConsole && appLog)
                {
                    output = new LogWriter();
                }

                // Define the UA Client application
                ApplicationInstance.MessageDlg = new ApplicationMessageDlg(output);
                CertificatePasswordProvider PasswordProvider = new CertificatePasswordProvider(password);
                ApplicationInstance application = new ApplicationInstance {
                    ApplicationName = applicationName,
                    ApplicationType = ApplicationType.Client,
                    ConfigSectionName = configSectionName,
                    CertificatePasswordProvider = PasswordProvider
                };

                // load the application configuration.
                var config = await application.LoadApplicationConfiguration(silent: false).ConfigureAwait(false);

                // override logfile
                if (logFile != null)
                {
                    var logFilePath = config.TraceConfiguration.OutputFilePath;
                    var filename = Path.GetFileNameWithoutExtension(logFilePath);
                    config.TraceConfiguration.OutputFilePath = logFilePath.Replace(filename, logFile);
                    config.TraceConfiguration.DeleteOnLoad = true;
                    config.TraceConfiguration.ApplySettings();
                }

                // setup the logging
                ConsoleUtils.ConfigureLogging(config, applicationName, logConsole, LogLevel.Information);

                // delete old certificate
                if (renewCertificate)
                {
                    await application.DeleteApplicationInstanceCertificate().ConfigureAwait(false);
                }

                // check the application certificate.
                bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, minimumKeySize: 0).ConfigureAwait(false);
                if (!haveAppCertificate)
                {
                    throw new ErrorExitException("Application instance certificate invalid!", ExitCode.ErrorCertificate);
                }

                if (reverseConnectUrlString != null)
                {
                    // start the reverse connection manager
                    output.WriteLine("Create reverse connection endpoint at {0}.", reverseConnectUrlString);
                    reverseConnectManager = new ReverseConnectManager();
                    reverseConnectManager.AddEndpoint(new Uri(reverseConnectUrlString));
                    reverseConnectManager.StartService(config);
                }

                // wait for timeout or Ctrl-C
                var quitCTS = new CancellationTokenSource();
                var quitEvent = ConsoleUtils.CtrlCHandler(quitCTS);

                // connect to a server until application stops
                bool quit = false;
                DateTime start = DateTime.UtcNow;
                int waitTime = int.MaxValue;
                {
                    do
                    {
                        if (timeout > 0)
                        {
                            waitTime = timeout - (int)DateTime.UtcNow.Subtract(start).TotalMilliseconds;
                            if (waitTime <= 0)
                            {
                                break;
                            }
                        }

                        // create the UA Client object and connect to configured server.

                        using (UAClient uaClient = new UAClient(application.ApplicationConfiguration, reverseConnectManager, output, ClientBase.ValidateResponse) {
                            AutoAccept = autoAccept,
                            SessionLifeTime = 60_000,
                        })
                        {
                            // set user identity
                            if (!String.IsNullOrEmpty(username))
                            {
                                uaClient.UserIdentity = new UserIdentity(username, userpassword ?? string.Empty);
                            }

                            bool connected = await uaClient.ConnectAsync(serverUrl.ToString(), !noSecurity, quitCTS.Token).ConfigureAwait(false);
                            if (connected)
                            {
                                output.WriteLine("Connected! Ctrl-C to quit.");

                                // enable subscription transfer
                                uaClient.ReconnectPeriod = 1000;
                                uaClient.ReconnectPeriodExponentialBackoff = 10000;
                                uaClient.Session.MinPublishRequestCount = 3;
                                uaClient.Session.TransferSubscriptionsOnReconnect = true;
                                var samples = new ClientSamples(output, ClientBase.ValidateResponse, quitEvent, verbose);
                                if (loadTypes)
                                {
                                    await samples.LoadTypeSystemAsync(uaClient.Session).ConfigureAwait(false);
                                }

                                if (browseall || fetchall || jsonvalues)
                                {
                                    NodeIdCollection variableIds = null;
                                    ReferenceDescriptionCollection referenceDescriptions = null;
                                    if (browseall)
                                    {
                                        referenceDescriptions =
                                            await samples.BrowseFullAddressSpaceAsync(uaClient, Objects.RootFolder).ConfigureAwait(false);
                                        variableIds = new NodeIdCollection(referenceDescriptions
                                            .Where(r => r.NodeClass == NodeClass.Variable && r.TypeDefinition.NamespaceIndex != 0)
                                            .Select(r => ExpandedNodeId.ToNodeId(r.NodeId, uaClient.Session.NamespaceUris)));
                                    }
                                    IList<INode> allNodes = null;
                                    if (fetchall)
                                    {
                                        allNodes = await samples.FetchAllNodesNodeCacheAsync(uaClient, Objects.RootFolder, true, true, false).ConfigureAwait(false);
                                        variableIds = new NodeIdCollection(allNodes
                                            .Where(r => r.NodeClass == NodeClass.Variable && r is VariableNode && ((VariableNode)r).DataType.NamespaceIndex != 0)
                                            .Select(r => ExpandedNodeId.ToNodeId(r.NodeId, uaClient.Session.NamespaceUris)));
                                    }

                                    if (assets)
                                    {
                                        // cache all ObjectTypes
                                        var objectNodes = await samples.FetchAllNodesNodeCacheAsync(
                                                uaClient, ObjectIds.ObjectTypesFolder, true, true, false, !fetchall).ConfigureAwait(false);

                                        // get all subtypes of IVendorNamePlate, ITagNameplateType, IDeviceHealthType
                                        var nodeCache = uaClient.Session.NodeCache;
                                        var namespaceIndex = uaClient.Session.NamespaceUris.GetIndex(Opc.Ua.DI.Namespaces.OpcUaDI);
                                        if (namespaceIndex > 0)
                                        {
                                            ushort opcUaDI = (ushort)namespaceIndex;
                                            var componentTypeSubTypes = await FetchSubTypesAsync(uaClient.Session, Opc.Ua.DI.ObjectTypeIds.ComponentType).ConfigureAwait(false);
                                            var vendorNameplateTypeSubTypes = await FetchSubTypesAsync(uaClient.Session, Opc.Ua.DI.ObjectTypeIds.IVendorNameplateType).ConfigureAwait(false);
                                            var tagNameplateTypeSubTypes = await FetchSubTypesAsync(uaClient.Session, Opc.Ua.DI.ObjectTypeIds.ITagNameplateType).ConfigureAwait(false);
                                            var deviceHealthSubTypes = await FetchSubTypesAsync(uaClient.Session, Opc.Ua.DI.ObjectTypeIds.IDeviceHealthType).ConfigureAwait(false);
                                            List<List<ObjectTypeNode>> interfaceTypes = new List<List<ObjectTypeNode>>()
                                            {
                                                    vendorNameplateTypeSubTypes, tagNameplateTypeSubTypes, deviceHealthSubTypes
                                                };
                                            var identification = new QualifiedName(Opc.Ua.DI.BrowseNames.Identification, opcUaDI);
                                            var deviceHealth = new QualifiedName(Opc.Ua.DI.BrowseNames.DeviceHealth, opcUaDI);

                                            // find all object types that implement a DI interface
                                            foreach (var node in objectNodes)
                                            {
                                                if (node is ObjectTypeNode objectTypeNode)
                                                {
                                                    bool foundSubTypes = false;
                                                    var hasInterfaces = objectTypeNode.ReferenceTable.Where(r => r.ReferenceTypeId.Equals(ReferenceTypeIds.HasInterface)).ToList();
                                                    foreach (var nt in hasInterfaces)
                                                    {
                                                        foreach (var interfaceSubTypes in interfaceTypes)
                                                        {
                                                            var newTypeDefinitions = interfaceSubTypes.FirstOrDefault(n => n.NodeId.Equals(nt.TargetId));
                                                            if (newTypeDefinitions != null)
                                                            {
                                                                var newSubTypes = await FetchSubTypesAsync(uaClient.Session, objectTypeNode.NodeId).ConfigureAwait(false);
                                                                componentTypeSubTypes.AddRange(newSubTypes);
                                                                foundSubTypes = true;
                                                                break;
                                                            }
                                                        }
                                                        if (foundSubTypes)
                                                        {
                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            // distinct on Componentypes
                                            NodeComparer nodeComparer = new NodeComparer();
                                            componentTypeSubTypes = componentTypeSubTypes.Distinct(nodeComparer).Select(o => (ObjectTypeNode)o).ToList();

                                            int count = 0;
                                            Console.WriteLine("Found {0} component object types implementing an interface.", componentTypeSubTypes.Count);
                                            foreach (var componentTypeSubType in componentTypeSubTypes)
                                            {
                                                Console.WriteLine("{0}: {1}: {2}", count++, componentTypeSubType.DisplayName, componentTypeSubType.IsAbstract ? "IsAbstract" : "");
                                            }

                                            if (browseall || fetchall)
                                            {
                                                IList<Node> allObjectNodes = null;

                                                if (fetchall)
                                                {
                                                    List<INode> allObjects = allNodes.Where(r => r.NodeClass == NodeClass.Object && r.NodeId.NamespaceIndex != 0).ToList();
                                                    allObjectNodes = allObjects.Select(o => (Node)o).ToList();
                                                }
                                                else
                                                {
                                                    List<ReferenceDescription> allObjects = referenceDescriptions.Where(r => r.NodeClass == NodeClass.Object && r.NodeId.NamespaceIndex != 0).ToList();
                                                    allObjectNodes = nodeCache.FetchNodes(allObjects.Select(o => o.NodeId).ToList());
                                                }

                                                foreach (var objectNode in allObjectNodes)
                                                {
                                                    bool parent = false;
                                                    if (objectNode.BrowseName == identification)
                                                    {
                                                        Console.WriteLine("Ident: {0} {1} Type: {2} ", objectNode.NodeId, objectNode.DisplayName, identification);
                                                        parent = true;
                                                    }

                                                    if (objectNode.BrowseName == deviceHealth)
                                                    {
                                                        Console.WriteLine("Health: {0} {1} Type: {2} ", objectNode.NodeId, objectNode.DisplayName, deviceHealth);
                                                        parent = true;
                                                    }


                                                    var hasTypeDefinition = objectNode.ReferenceTable.FirstOrDefault(r => r.ReferenceTypeId.Equals(ReferenceTypeIds.HasTypeDefinition));
                                                    if (hasTypeDefinition != null)
                                                    {
                                                        var targetId = ExpandedNodeId.ToNodeId(hasTypeDefinition.TargetId, uaClient.Session.NamespaceUris);
                                                        var componentType = componentTypeSubTypes.FirstOrDefault(c => c.NodeId.Equals(targetId));
                                                        if (componentType != null)
                                                        {
                                                            var node = nodeCache.Find(targetId);
                                                            Console.WriteLine("Ident: {0} {1} Type: {2} ", objectNode.NodeId, objectNode.DisplayName, node.DisplayName);
                                                            parent = true;
                                                        }
                                                    }

                                                    if (parent)
                                                    {
                                                        var parentObject = nodeCache.FindReferences(objectNode.NodeId, ReferenceTypeIds.HierarchicalReferences, true, true).FirstOrDefault();
                                                        var parentNode = nodeCache.Find(parentObject.NodeId);
                                                        Console.WriteLine("Parent Asset: {0}", parentNode.DisplayName);
                                                        var properties = objectNode.ReferenceTable.Where(r => r.ReferenceTypeId.Equals(ReferenceTypeIds.HasProperty));
                                                        var propertyNodes = nodeCache.Find(properties.Select(r => r.TargetId).ToList()).Select(n => (Node)n);
                                                        uaClient.Session.ReadValues(propertyNodes.Select(n => n.NodeId).ToList(), out var propertyValues, out var serviceResults);
                                                        int ii = 0;
                                                        foreach (var propertyNode in propertyNodes)
                                                        {
                                                            if (ServiceResult.IsGood(serviceResults[ii]))
                                                            {
                                                                Variant propertyValue = propertyValues[ii].WrappedValue;
                                                                Console.WriteLine("  {0}:{1}", propertyNode.DisplayName, propertyValue);
                                                            }
                                                            else
                                                            {
                                                                Console.WriteLine("  {0}:{1}", propertyNode.DisplayName, serviceResults[ii].StatusCode);
                                                            }
                                                            ii++;
                                                        }
                                                    }

                                                    var hasInterface = objectNode.ReferenceTable.FirstOrDefault(r => r.ReferenceTypeId.Equals(ReferenceTypeIds.HasInterface));
                                                    if (hasInterface != null)
                                                    {
                                                        var targetId = ExpandedNodeId.ToNodeId(hasInterface.TargetId, uaClient.Session.NamespaceUris);
                                                        var vendorNamePlateTypes = vendorNameplateTypeSubTypes.FindAll(c => c.NodeId.Equals(targetId));
                                                        var tagNameplateTypeTypes = tagNameplateTypeSubTypes.FindAll(c => c.NodeId.Equals(targetId));
                                                        var deviceHealthTypes = deviceHealthSubTypes.FindAll(c => c.NodeId.Equals(targetId));

                                                        if (vendorNamePlateTypes.Count > 0)
                                                        {
                                                            var node = nodeCache.Find(targetId);
                                                            Console.WriteLine("Vendor: {0} Type: {1} ", vendorNamePlateTypes[0].DisplayName, node.DisplayName);
                                                        }
                                                        if (tagNameplateTypeTypes.Count > 0)
                                                        {
                                                            var node = nodeCache.Find(targetId);
                                                            Console.WriteLine("Tag: {0} Type: {1} ", tagNameplateTypeTypes[0].DisplayName, node.DisplayName);
                                                        }
                                                        if (deviceHealthTypes.Count > 0)
                                                        {
                                                            var node = nodeCache.Find(targetId);
                                                            Console.WriteLine("DeviceHealth: {0} Type: {1} ", deviceHealthTypes[0].DisplayName, node.DisplayName);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (jsonvalues && variableIds != null)
                                    {
                                        var (allValues, results) = await samples.ReadAllValuesAsync(uaClient, variableIds).ConfigureAwait(false);
                                    }

                                    if (subscribe && (browseall || fetchall))
                                    {
                                        // subscribe to 100 random variables
                                        const int MaxVariables = 100;
                                        NodeCollection variables = new NodeCollection();
                                        Random random = new Random(62541);
                                        if (fetchall)
                                        {
                                            variables.AddRange(allNodes
                                                .Where(r => r.NodeClass == NodeClass.Variable && r.NodeId.NamespaceIndex > 1)
                                                .Select(r => ((VariableNode)r))
                                                .OrderBy(o => random.Next())
                                                .Take(MaxVariables));
                                        }
                                        else if (browseall)
                                        {
                                            var variableReferences = referenceDescriptions
                                                .Where(r => r.NodeClass == NodeClass.Variable && r.NodeId.NamespaceIndex > 1)
                                                .Select(r => r.NodeId)
                                                .OrderBy(o => random.Next())
                                                .Take(MaxVariables)
                                                .ToList();
                                            variables.AddRange(uaClient.Session.NodeCache.Find(variableReferences).Cast<Node>());
                                        }

                                        await samples.SubscribeAllValuesAsync(uaClient,
                                            variableIds: new NodeCollection(variables),
                                            samplingInterval: 1000,
                                            publishingInterval: 5000,
                                            queueSize: 10,
                                            lifetimeCount: 12,
                                            keepAliveCount: 2).ConfigureAwait(false);

                                        // Wait for DataChange notifications from MonitoredItems
                                        output.WriteLine("Subscribed to {0} variables. Press Ctrl-C to exit.", MaxVariables);
                                        quit = quitEvent.WaitOne(timeout > 0 ? waitTime : Timeout.Infinite);
                                    }
                                    else
                                    {
                                        quit = true;
                                    }
                                }
                                else
                                {
                                    // Run tests for available methods on reference server.
                                    samples.ReadNodes(uaClient.Session);
                                    samples.WriteNodes(uaClient.Session);
                                    samples.Browse(uaClient.Session);
                                    samples.CallMethod(uaClient.Session);
                                    samples.SubscribeToDataChanges(uaClient.Session, 120_000);

                                    output.WriteLine("Waiting...");

                                    // Wait for some DataChange notifications from MonitoredItems
                                    quit = quitEvent.WaitOne(timeout > 0 ? waitTime : 30_000);
                                }

                                output.WriteLine("Client disconnected.");

                                uaClient.Disconnect();
                            }
                            else
                            {
                                output.WriteLine("Could not connect to server! Retry in 10 seconds or Ctrl-C to quit.");
                                quit = quitEvent.WaitOne(Math.Min(10_000, waitTime));
                            }
                        }
                    } while (!quit);

                    output.WriteLine("Client stopped.");
                }
            }
            catch (Exception ex)
            {
                output.WriteLine(ex.Message);
            }
        }

        private static async Task<List<ObjectTypeNode>> FetchSubTypesAsync(Opc.Ua.Client.ISession session, ExpandedNodeId startNode)
        {
            var nodeCache = session.NodeCache;
            var node = await nodeCache.FetchNodeAsync(startNode).ConfigureAwait(false);
            var allSubTypeNodes = new List<Node>() { node };
            var rootTypes = new List<ExpandedNodeId>() { startNode };
            while (rootTypes.Count > 0)
            {
                var nextRootTypesNodeId = new List<NodeId>();
                foreach (var rootType in rootTypes)
                {
                    IList<NodeId> subTypes = nodeCache.FindSubTypes(rootType);
                    nextRootTypesNodeId.AddRange(subTypes);
                    var subTypesNodes = await nodeCache.FetchNodesAsync(subTypes.Select(n => NodeId.ToExpandedNodeId(n, session.NamespaceUris)).ToList()).ConfigureAwait(false);
                    allSubTypeNodes.AddRange(subTypesNodes);
                }
                rootTypes = nextRootTypesNodeId.Select(nodeId => NodeId.ToExpandedNodeId(nodeId, session.NamespaceUris)).ToList();
            }
            return allSubTypeNodes.Select(o => (ObjectTypeNode)o).ToList();
        }
    }

    public class NodeComparer : IEqualityComparer<Node>
    {
        public bool Equals(Node x, Node y)
        {
            if (Object.ReferenceEquals(x, y)) return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            if (x.NodeId == y.NodeId)
            {
                return true;
            }

            return false;
        }

        public int GetHashCode(Node node)
        {
            if (Object.ReferenceEquals(node, null)) return 0;
            return node.NodeId.GetHashCode();
        }
    }
}
