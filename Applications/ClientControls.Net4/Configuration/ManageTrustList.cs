using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Opc.Ua.Client.Controls.Configuration
{
    public partial class ManageTrustList : Form
    {
        private enum TrustListType
        {
            Application,
            Https,
            User
        }

        public ManageTrustList()
        {
            InitializeComponent();
        }

        private ApplicationConfiguration m_configuration;
        private TrustListType m_trustListType;

        #region Public Interface
        /// <summary>
        /// Displays the dialog.
        /// </summary>
        public void ShowDialog(ApplicationConfiguration configuration)
        {
            m_configuration = configuration;
            buttonApplication_Click(null, null);
            ShowDialog();
        }
        #endregion

        private void buttonApplication_Click(object sender, EventArgs e)
        {
            this.Name = "Manage Application Certificate Trust List";
            m_trustListType = TrustListType.Application;
            trustListControl.Initialize(
                m_configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath,
                m_configuration.SecurityConfiguration.TrustedIssuerCertificates.StorePath,
                m_configuration.SecurityConfiguration.RejectedCertificateStore.StorePath);
        }

        private void buttonHttps_Click(object sender, EventArgs e)
        {
            m_trustListType = TrustListType.Https;
            trustListControl.Initialize(
                m_configuration.SecurityConfiguration.TrustedHttpsCertificates.StorePath,
                m_configuration.SecurityConfiguration.HttpsIssuerCertificates.StorePath,
                m_configuration.SecurityConfiguration.RejectedCertificateStore.StorePath);
        }

        private void buttonUser_Click(object sender, EventArgs e)
        {
            m_trustListType = TrustListType.User;
            trustListControl.Initialize(
                m_configuration.SecurityConfiguration.TrustedUserCertificates.StorePath,
                m_configuration.SecurityConfiguration.UserIssuerCertificates.StorePath,
                m_configuration.SecurityConfiguration.RejectedCertificateStore.StorePath);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            switch (m_trustListType)
            {
                case TrustListType.Https:
                    break;
            }
        }
    }
}
