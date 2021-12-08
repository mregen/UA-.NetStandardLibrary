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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Configuration;

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
            Console.WriteLine("OPC UA Console Reference Client");
            IOutput log = new ConsoleOutput();

            // The application name and config file names
            var applicationName = "ConsoleReferenceClient";
            var configSectionName = "Quickstarts.ReferenceClient";
            var usage = $"Usage: dotnet {applicationName}.dll [OPTIONS]";

            // command line options
            bool showHelp = false;
            bool autoAccept = false;
            bool logConsole = false;
            bool renewCertificate = false;
            string password = null;
            int timeout = -1;

            Mono.Options.OptionSet options = new Mono.Options.OptionSet {
                { "h|help", "show this message and exit", h => showHelp = h != null },
                { "a|autoaccept", "auto accept certificates (for testing only)", a => autoAccept = a != null },
                { "c|console", "log trace to console", c => logConsole = c != null },
                { "p|password=", "optional password for private key", (string p) => password = p },
                { "r|renew", "renew application certificate", r => renewCertificate = r != null },
                { "t|timeout=", "timeout in seconds to exit application", (int t) => timeout = t * 1000 },
            };

            try
            {
                // parse command line and set options
                var extraArg = ConsoleUtils.ProcessCommandLine(args, options, ref showHelp, usage, false);

                // connect Url?
                Uri serverUrl = new Uri("opc.tcp://localhost:62541/Quickstarts/ReferenceServer");
                if (!string.IsNullOrEmpty(extraArg))
                {
                    serverUrl = new Uri(extraArg);
                }

                // use ILogger as output
                if (logConsole)
                {
                    log = new LogOutput();
                }

                // Define the UA Client application
                ApplicationInstance.MessageDlg = new ApplicationMessageDlg();
                CertificatePasswordProvider PasswordProvider = new CertificatePasswordProvider(password);
                ApplicationInstance application = new ApplicationInstance {
                    ApplicationName = applicationName,
                    ApplicationType = ApplicationType.Client,
                    ConfigSectionName = configSectionName,
                    CertificatePasswordProvider = PasswordProvider
                };

                // load the application configuration.
                var config = await application.LoadApplicationConfiguration(silent: false);

                if (renewCertificate)
                {
                    await application.DeleteApplicationInstanceCertificate().ConfigureAwait(false);
                }

                // check the application certificate.
                bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, minimumKeySize: 0).ConfigureAwait(false);
                if (!haveAppCertificate)
                {
                    throw new Exception("Application instance certificate invalid!");
                }

                // setup the logging
                ConsoleUtils.ConfigureLogging(config, applicationName, logConsole, LogLevel.Information);

                // wait for timeout or Ctrl-C
                var quitEvent = ConsoleUtils.CtrlCHandler();
                DateTime start = DateTime.UtcNow;

                // connect to a server until application stopped
                bool quit = false;
                do
                {
                    int waitTime = timeout - (int)DateTime.UtcNow.Subtract(start).TotalMilliseconds;
                    if (waitTime <= 0)
                    {
                        break;
                    }

                    // create the UA Client object and connect to configured server.
                    UAClient uaClient = new UAClient(application.ApplicationConfiguration, log, ClientBase.ValidateResponse) {
                        AutoAccept = autoAccept
                    };

                    bool connected = await uaClient.ConnectAsync(serverUrl.ToString());
                    if (connected)
                    {
                        // Run tests for available methods.
                        uaClient.ReadNodes();
                        uaClient.WriteNodes();
                        uaClient.Browse();
                        uaClient.CallMethod();

                        uaClient.SubscribeToDataChanges();

                        // Wait for some DataChange notifications from MonitoredItems
                        quit = quitEvent.WaitOne(Math.Min(30_000, waitTime));

                        uaClient.Disconnect();
                    }
                    else
                    {
                        log.WriteLine("Could not connect to server! Retry in 10 seconds or Ctrl-C to quit.");
                        quit = quitEvent.WaitOne(Math.Min(10_000, waitTime));
                    }

                } while (!quit);

                log.WriteLine("\nClient stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
