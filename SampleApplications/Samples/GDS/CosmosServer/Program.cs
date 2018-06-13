/* ========================================================================
 * Copyright (c) 2005-2017 The OPC Foundation, Inc. All rights reserved.
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
using Opc.Ua.Configuration;
using Opc.Ua.Gds.Server.Database.CosmosDB;
using Opc.Ua.Gds.Server.Database.Linq;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Opc.Ua.Gds.Server
{
    public class ApplicationMessageDlg : IApplicationMessageDlg
    {
        private string message = string.Empty;
        private bool ask = false;

        public override void Message(string text, bool ask)
        {
            this.message = text;
            this.ask = ask;
        }

        public override async Task<bool> ShowAsync()
        {
            if (ask)
            {
                message += " (y/n, default y): ";
                Console.Write(message);
            }
            else
            {
                Console.WriteLine(message);
            }
            if (ask)
            {
                try
                {
                    ConsoleKeyInfo result = Console.ReadKey();
                    Console.WriteLine();
                    return await Task.FromResult((result.KeyChar == 'y') || (result.KeyChar == 'Y') || (result.KeyChar == '\r'));
                }
                catch
                {
                    // intentionally fall through
                }
            }
            return await Task.FromResult(true);
        }
    }

    public enum ExitCode : int
    {
        Ok = 0,
        ErrorServerNotStarted = 0x80,
        ErrorServerRunning = 0x81,
        ErrorServerException = 0x82,
        ErrorInvalidCommandLine = 0x100
    };

    public class Program
    {

        public static int Main(string[] args)
        {
            Console.WriteLine(".Net Core Edge OPC UA Global Discovery Server");

            // command line options
            bool showHelp = false;
            string gdsVault = null;
            string appID = null;
            string opcTwin = null;

            Mono.Options.OptionSet options = new Mono.Options.OptionSet {
                { "g|gdsvault=", "GDS Vault Url", g => gdsVault = g },
                { "a|appid=", "Active Directory Application Id", a => appID = a },
                { "o|opctwin=", "OPC Twin Url", o => opcTwin = o },
                { "h|help", "show this message and exit", h => showHelp = h != null },
            };

            try
            {
                IList<string> extraArgs = options.Parse(args);
                foreach (string extraArg in extraArgs)
                {
                    Console.WriteLine("Error: Unknown option: {0}", extraArg);
                    showHelp = true;
                }
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                showHelp = true;
            }

            if (showHelp)
            {
                Console.WriteLine("Usage: dotnet EdgeGlobalDiscoveryServer.dll [OPTIONS]");
                Console.WriteLine();

                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return (int)ExitCode.ErrorInvalidCommandLine;
            }

            CloudGlobalDiscoveryServer server = new CloudGlobalDiscoveryServer();
            server.Run(gdsVault, appID, opcTwin);

            return (int)CloudGlobalDiscoveryServer.ExitCode;
        }
    }

    public class CloudGlobalDiscoveryServer
    {
        GlobalDiscoverySampleServer server;
        Task status;
        DateTime lastEventTime;
        static ExitCode exitCode;

        public CloudGlobalDiscoveryServer()
        {
        }

        public void Run(string gdsVault, string appID, string opcTwin)
        {

            try
            {
                exitCode = ExitCode.ErrorServerNotStarted;
                ConsoleGlobalDiscoveryServer(gdsVault, appID, opcTwin).Wait();
                Console.WriteLine("Server started. Press Ctrl-C to exit...");
                exitCode = ExitCode.ErrorServerRunning;
            }
            catch (Exception ex)
            {
                Utils.Trace("ServiceResultException:" + ex.Message);
                Console.WriteLine("Exception: {0}", ex.Message);
                exitCode = ExitCode.ErrorServerException;
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
            quitEvent.WaitOne();

            if (server != null)
            {
                Console.WriteLine("Server stopped. Waiting for exit...");

                using (GlobalDiscoverySampleServer _server = server)
                {
                    // Stop status thread
                    server = null;
                    status.Wait();
                    // Stop server and dispose
                    _server.Stop();
                }
            }

            exitCode = ExitCode.Ok;
        }

        public static ExitCode ExitCode { get => exitCode; }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                // GDS accepts any client certificate
                e.Accept = true;
                Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
            }
        }

        private async Task ConsoleGlobalDiscoveryServer(string gdsVaultServiceUrl, string appId, string opcTwinServiceUrl)
        {
            ApplicationInstance.MessageDlg = new ApplicationMessageDlg();
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "CosmosDB Global Discovery Server",
                ApplicationType = ApplicationType.Server,
                ConfigSectionName = "Opc.Ua.CosmosDBGlobalDiscoveryServer"
            };

            // load the application configuration.
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);

            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (!config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }

            // get the DatabaseStorePath configuration parameter.
            GlobalDiscoveryServerConfiguration gdsConfiguration = config.ParseExtension<GlobalDiscoveryServerConfiguration>();

            // extract appId and vault name from database storage path
            string[] keyVaultConfig = gdsConfiguration.DatabaseStorePath?.Split(',');
            if (keyVaultConfig != null)
            {
                if (String.IsNullOrEmpty(gdsVaultServiceUrl))
                {
                    // try configuration using XML config
                    gdsVaultServiceUrl = keyVaultConfig[0];
                }

                if (String.IsNullOrEmpty(appId))
                {
                    if (keyVaultConfig.Length > 1 && !String.IsNullOrEmpty(keyVaultConfig[1]))
                    {
                        appId = keyVaultConfig[1];
                    }
                }

                if (String.IsNullOrEmpty(opcTwinServiceUrl))
                {
                    if (keyVaultConfig.Length > 1)
                    {
                        // initialize database and certificate group handler
                        opcTwinServiceUrl = "http://localhost:9042/v1";
                        if (keyVaultConfig.Length == 3)
                        {
                            opcTwinServiceUrl = keyVaultConfig[2];
                        }
                    }
                }
            }

            // The vault handler with authentication
            var gdsVaultHandler = new OpcGdsVaultClientHandler(gdsVaultServiceUrl);
            if (String.IsNullOrEmpty(appId))
            {
                // authenticate key vault with MSI (web app) or developer user account
                gdsVaultHandler.SetTokenProvider();
            }
            else
            {
                // authenticate key vault with app cert
                gdsVaultHandler.SetAssertionCertificate(appId, await config.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(string.Empty));
            }

            // read configurations from GDS Vault
            gdsConfiguration.CertificateGroups = await gdsVaultHandler.GetCertificateConfigurationGroupsAsync(gdsConfiguration.BaseCertificateGroupStorePath);

            UpdateGDSConfigurationDocument(config.Extensions, gdsConfiguration);

            var certGroup = new GdsVaultCertificateGroup(gdsVaultHandler);
#if mist
            if (!String.IsNullOrEmpty(opcTwinServiceUrl))
            {
                if (!opcTwinServiceUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                {
                    opcTwinServiceUrl += "/v1";
                }
                // initialize database and certificate group handler
                var database = new OpcTwinApplicationsDatabase(opcTwinServiceUrl);
                server = new GlobalDiscoverySampleServer(database, database, certGroup);
            }
            else
#endif
            {
                var database = new CosmosDBApplicationsDatabase();
                database.Initialize();
                server = new GlobalDiscoverySampleServer(database, database, certGroup);
            }

            // start the server.
            await application.Start(server);

            // print endpoint info
            var endpoints = application.Server.GetEndpoints().Select(e => e.EndpointUrl).Distinct();
            foreach (var endpoint in endpoints)
            {
                Console.WriteLine(endpoint);
            }

            // start the status thread
            status = Task.Run(new Action(StatusThread));

            // print notification on session events
            server.CurrentInstance.SessionManager.SessionActivated += EventStatus;
            server.CurrentInstance.SessionManager.SessionClosing += EventStatus;
            server.CurrentInstance.SessionManager.SessionCreated += EventStatus;

        }

        /// <summary>
        /// Updates the config extension with the new configuration information.
        /// </summary>
        private static void UpdateGDSConfigurationDocument(XmlElementCollection extensions, GlobalDiscoveryServerConfiguration gdsConfiguration)
        {
            XmlDocument gdsDoc = new XmlDocument();
            var qualifiedName = EncodeableFactory.GetXmlName(typeof(GlobalDiscoveryServerConfiguration));
            XmlSerializer gdsSerializer = new XmlSerializer(typeof(GlobalDiscoveryServerConfiguration), qualifiedName.Namespace);
            using (XmlWriter writer = gdsDoc.CreateNavigator().AppendChild())
            {
                gdsSerializer.Serialize(writer, gdsConfiguration);
            }

            foreach (var extension in extensions)
            {
                if (extension.Name == qualifiedName.Name)
                {
                    extension.InnerXml = gdsDoc.DocumentElement.InnerXml;
                }
            }
        }


        private void EventStatus(Session session, SessionEventReason reason)
        {
            lastEventTime = DateTime.UtcNow;
            PrintSessionStatus(session, reason.ToString());
        }

        void PrintSessionStatus(Session session, string reason, bool lastContact = false)
        {
            lock (session.DiagnosticsLock)
            {
                string item = String.Format("{0,9}:{1,20}:", reason, session.SessionDiagnostics.SessionName);
                if (lastContact)
                {
                    item += String.Format("Last Event:{0:HH:mm:ss}", session.SessionDiagnostics.ClientLastContactTime.ToLocalTime());
                }
                else
                {
                    if (session.Identity != null)
                    {
                        item += String.Format(":{0,20}", session.Identity.DisplayName);
                    }
                    item += String.Format(":{0}", session.Id);
                }
                Console.WriteLine(item);
            }
        }

        private async void StatusThread()
        {
            while (server != null)
            {
                if (DateTime.UtcNow - lastEventTime > TimeSpan.FromMilliseconds(6000))
                {
                    IList<Session> sessions = server.CurrentInstance.SessionManager.GetSessions();
                    for (int ii = 0; ii < sessions.Count; ii++)
                    {
                        Session session = sessions[ii];
                        PrintSessionStatus(session, "-Status-", true);
                    }
                    lastEventTime = DateTime.UtcNow;
                }
                await Task.Delay(1000);
            }
        }
    }
}
