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

using Mono.Options;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Globalization;

namespace Quickstarts.ReferenceServer
{
    /// <summary>
    /// The program.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            TextWriter output = Console.Out;
            output.WriteLine("{0} OPC UA Reference Server", Utils.IsRunningOnMono() ? "Mono" : ".NET Core");

            output.WriteLine("OPC UA library: {0} @ {1} -- {2}",
                Utils.GetAssemblyBuildNumber(),
                Utils.GetAssemblyTimestamp().ToString("G", CultureInfo.InvariantCulture),
                Utils.GetAssemblySoftwareVersion());

            // The application name and config file names
            var applicationName = Utils.IsRunningOnMono() ? "MonoReferenceServer" : "ConsoleReferenceServer";
            var configSectionName = Utils.IsRunningOnMono() ? "Quickstarts.MonoReferenceServer" : "Quickstarts.ReferenceServer";

            // command line options
            bool showHelp = false;
            bool autoAccept = false;
            bool logConsole = false;
            bool appLog = false;
            bool renewCertificate = false;
            bool shadowConfig = false;
            bool cttMode = false;
            string password = null;
            int timeout = -1;

            var usage = Utils.IsRunningOnMono() ? $"Usage: mono {applicationName}.exe [OPTIONS]" : $"Usage: dotnet {applicationName}.dll [OPTIONS]";
            Mono.Options.OptionSet options = new Mono.Options.OptionSet {
                usage,
                { "h|help", "show this message and exit", h => showHelp = h != null },
                { "a|autoaccept", "auto accept certificates (for testing only)", a => autoAccept = a != null },
                { "c|console", "log to console", c => logConsole = c != null },
                { "l|log", "log app output", c => appLog = c != null },
                { "p|password=", "optional password for private key", (string p) => password = p },
                { "r|renew", "renew application certificate", r => renewCertificate = r != null },
                { "t|timeout=", "timeout in seconds to exit application", (int t) => timeout = t * 1000 },
                { "s|shadowconfig", "create configuration in pki root", s => shadowConfig = s != null },
                { "ctt", "CTT mode, use to preset alarms for CTT testing.", c => cttMode = c != null },
            };

            try
            {
                // parse command line and set options
                ConsoleUtils.ProcessCommandLine(output, args, options, ref showHelp);

                if (logConsole && appLog)
                {
                    output = new LogWriter();
                }

                // create the UA server
                var server = new UAServer<ReferenceServer>(output) {
                    AutoAccept = autoAccept,
                    Password = password
                };

                // load the server configuration, validate certificates
                output.WriteLine("Loading configuration from {0}.", configSectionName);
                await server.LoadAsync(applicationName, configSectionName).ConfigureAwait(false);

                // use the shadow config to map the config to an externally accessible location
                if (shadowConfig)
                {
                    output.WriteLine("Using shadow configuration.");
                    var shadowPath = Directory.GetParent(Path.GetDirectoryName(
                        Utils.ReplaceSpecialFolderNames(server.Configuration.TraceConfiguration.OutputFilePath))).FullName;
                    var shadowFilePath = Path.Combine(shadowPath, Path.GetFileName(server.Configuration.SourceFilePath));
                    if (!File.Exists(shadowFilePath))
                    {
                        output.WriteLine("Create a copy of the config in the shadow location.");
                        File.Copy(server.Configuration.SourceFilePath, shadowFilePath, true);
                    }
                    output.WriteLine("Reloading configuration from {0}.", shadowFilePath);
                    await server.LoadAsync(applicationName, Path.Combine(shadowPath, configSectionName)).ConfigureAwait(false);
                }

                // setup the logging
                ConsoleUtils.ConfigureLogging(server.Configuration, applicationName, logConsole, LogLevel.Information);

                // check or renew the certificate
                output.WriteLine("Check the certificate.");
                await server.CheckCertificateAsync(renewCertificate).ConfigureAwait(false);

                // Create and add the node managers
                server.Create(Servers.Utils.NodeManagerFactories);

                // start the server
                output.WriteLine("Start the server.");
                await server.StartAsync().ConfigureAwait(false);


                output.WriteLine("Server started. Press Ctrl-C to exit...");

                // wait for timeout or Ctrl-C
                var quitEvent = ConsoleUtils.CtrlCHandler();
                bool ctrlc = quitEvent.WaitOne(timeout);

                // stop server. May have to wait for clients to disconnect.
                output.WriteLine("Server stopped. Waiting for exit...");
                await server.StopAsync().ConfigureAwait(false);

                return (int)ExitCode.Ok;
            }
            catch (ErrorExitException eee)
            {
                output.WriteLine("The application exits with error: {0}", eee.Message);
                return (int)eee.ExitCode;
            }
        }
    }
#if mist
    void ActivitySource()
{
            var activitySource = new ActivitySource("MySource");
            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MySample"))
                            .AddSource("MySource")
                            .AddConsoleExporter()
                            .Build();
            using (var activity = activitySource.StartActivity("ServerStart"))
            {
                activity?.Start();
                activity?.SetTag("First", m_server);
                await application.Start(m_server).ConfigureAwait(false);
                activity?.Stop();
            }

            var activityId = Activity.Current;

            using (var activity = activitySource.StartActivity("GetEndpoints"))
            {
                activity?.Start();

                // print endpoint info
                var endpoints = application.Server.GetEndpoints().Select(e => e.EndpointUrl).Distinct();
                activity?.SetTag("GetEndpoints", endpoints);

                foreach (var endpoint in endpoints)
                {
                    Console.WriteLine(endpoint);
                }
                activity?.Stop();
            }

            using (var activity = activitySource.StartActivity("Status"))
            {
                // start the status thread
                m_status = Task.Run(new Action(StatusThreadAsync));
            }

            // print notification on session events
            m_server.CurrentInstance.SessionManager.SessionActivated += EventStatus;
            m_server.CurrentInstance.SessionManager.SessionClosing += EventStatus;
            m_server.CurrentInstance.SessionManager.SessionCreated += EventStatus;

            // test the logging setup
            Utils.Trace(Utils.TraceMasks.Error      , "This is an Error message: {0}", Utils.TraceMasks.Error);
            Utils.Trace(Utils.TraceMasks.Information, "This is a Information message: {0}", Utils.TraceMasks.Information);
            Utils.Trace(Utils.TraceMasks.StackTrace , "This is a StackTrace message: {0}", Utils.TraceMasks.StackTrace);
            Utils.Trace(Utils.TraceMasks.Service, "This is a Service message: {0}", Utils.TraceMasks.Service);
            Utils.Trace(Utils.TraceMasks.ServiceDetail, "This is a ServiceDetail message: {0}", Utils.TraceMasks.ServiceDetail);
            Utils.Trace(Utils.TraceMasks.Operation, "This is a Operation message: {0}", Utils.TraceMasks.Operation);
            Utils.Trace(Utils.TraceMasks.OperationDetail, "This is a OperationDetail message: {0}", Utils.TraceMasks.OperationDetail);
            Utils.Trace(Utils.TraceMasks.StartStop, "This is a StartStop message: {0}", Utils.TraceMasks.StartStop);
            Utils.Trace(Utils.TraceMasks.ExternalSystem, "This is a ExternalSystem message: {0}", Utils.TraceMasks.ExternalSystem);
            Utils.Trace(Utils.TraceMasks.Security, "This is a Security message: {0}", Utils.TraceMasks.Security);

}

#endif
}


