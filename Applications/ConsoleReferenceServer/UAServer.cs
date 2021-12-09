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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace Quickstarts
{
    public class UAServer<T> where T : StandardServer, new()
    {
        public ApplicationInstance Application => m_application;
        public ApplicationConfiguration Configuration => m_application.ApplicationConfiguration;

        public bool LogConsole { get; set; } = false;
        public bool AutoAccept { get; set; } = false;
        public string Password { get; set; } = null;

        public ExitCode ExitCode { get; private set; }

        /// <summary>
        /// Load the application configuration.
        /// </summary>
        public async Task LoadAsync(string applicationName, string configSectionName)
        {
            try
            {
                ExitCode = ExitCode.ErrorNotStarted;

                ApplicationInstance.MessageDlg = new ApplicationMessageDlg();
                CertificatePasswordProvider PasswordProvider = new CertificatePasswordProvider(Password);
                m_application = new ApplicationInstance {
                    ApplicationName = applicationName,
                    ApplicationType = ApplicationType.Server,
                    ConfigSectionName = configSectionName,
                    CertificatePasswordProvider = PasswordProvider
                };

                // load the application configuration.
                await m_application.LoadApplicationConfiguration(false).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                throw new ErrorExitException(ex.Message, ExitCode);
            }
        }

        /// <summary>
        /// Load the application configuration.
        /// </summary>
        public async Task CheckCertificateAsync(bool renewCertificate)
        {
            try
            {
                var config = m_application.ApplicationConfiguration;
                if (renewCertificate)
                {
                    await m_application.DeleteApplicationInstanceCertificate().ConfigureAwait(false);
                }

                // check the application certificate.
                bool haveAppCertificate = await m_application.CheckApplicationInstanceCertificate(false, minimumKeySize: 0).ConfigureAwait(false);
                if (!haveAppCertificate)
                {
                    throw new Exception("Application instance certificate invalid!");
                }

                if (!config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
                }
            }
            catch (Exception ex)
            {
                throw new ErrorExitException(ex.Message, ExitCode);
            }
        }

        /// <summary>
        /// Start the server.
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                // create the server.
                m_server = new T();

                // start the server
                await m_application.Start(m_server).ConfigureAwait(false);

                ExitCode = ExitCode.ErrorRunning;

                // print endpoint info
                var endpoints = m_application.Server.GetEndpoints().Select(e => e.EndpointUrl).Distinct();
                foreach (var endpoint in endpoints)
                {
                    Console.WriteLine(endpoint);
                }

                // start the status thread
                m_status = Task.Run(new Action(StatusThreadAsync));

                // print notification on session events
                m_server.CurrentInstance.SessionManager.SessionActivated += EventStatus;
                m_server.CurrentInstance.SessionManager.SessionClosing += EventStatus;
                m_server.CurrentInstance.SessionManager.SessionCreated += EventStatus;
            }
            catch (Exception ex)
            {
                throw new ErrorExitException(ex.Message, ExitCode);
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (m_server != null)
                {
                    using (T server = m_server)
                    {
                        // Stop status thread
                        m_server = null;
                        await m_status;

                        // Stop server and dispose
                        server.Stop();
                    }
                }

                ExitCode = ExitCode.Ok;
            }
            catch (Exception ex)
            {
                throw new ErrorExitException(ex.Message, ExitCode.ErrorStopping);
            }
        }

        private void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                if (AutoAccept)
                {
                    if (!LogConsole)
                    {
                        Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                    }
                    Utils.LogCertificate("Accepted Certificate:", e.Certificate);
                    e.Accept = true;
                    return;
                }
            }

            if (!LogConsole)
            {
                Console.WriteLine("Rejected Certificate: {0} {1}", e.Error, e.Certificate.Subject);
            }

            Utils.LogCertificate(Utils.TraceMasks.Security, "Rejected Certificate: {0}", e.Certificate, e.Error);
        }

        private void EventStatus(Session session, SessionEventReason reason)
        {
            m_lastEventTime = DateTime.UtcNow;
            PrintSessionStatus(session, reason.ToString());
        }

        private void PrintSessionStatus(Session session, string reason, bool lastContact = false)
        {
            lock (session.DiagnosticsLock)
            {
                StringBuilder item = new StringBuilder();
                item.AppendFormat("{0,9}:{1,20}:", reason, session.SessionDiagnostics.SessionName);
                if (lastContact)
                {
                    item.AppendFormat("Last Event:{0:HH:mm:ss}", session.SessionDiagnostics.ClientLastContactTime.ToLocalTime());
                }
                else
                {
                    if (session.Identity != null)
                    {
                        item.AppendFormat(":{0,20}", session.Identity.DisplayName);
                    }
                    item.AppendFormat(":{0}", session.Id);
                }
                Utils.LogInfo(item.ToString());
            }
        }

        private async void StatusThreadAsync()
        {
            while (m_server != null)
            {
                if (DateTime.UtcNow - m_lastEventTime > TimeSpan.FromMilliseconds(6000))
                {
                    IList<Session> sessions = m_server.CurrentInstance.SessionManager.GetSessions();
                    for (int ii = 0; ii < sessions.Count; ii++)
                    {
                        Session session = sessions[ii];
                        PrintSessionStatus(session, "-Status-", true);
                    }
                    m_lastEventTime = DateTime.UtcNow;
                }
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        #region Private Members
        private ApplicationInstance m_application;
        private T m_server;
        private Task m_status;
        private DateTime m_lastEventTime;
        #endregion
    }
}

