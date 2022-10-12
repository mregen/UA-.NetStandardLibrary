
namespace Opc.Ua.Common.Controls.Configuration
{
    partial class ManageTrustList
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonApplication = new System.Windows.Forms.Button();
            this.buttonHttps = new System.Windows.Forms.Button();
            this.buttonUser = new System.Windows.Forms.Button();
            this.trustListControl = new Opc.Ua.Common.Controls.TrustListControl();
            this.SuspendLayout();
            // 
            // buttonOK
            // 
            this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(13, 518);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 1;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Enabled = false;
            this.buttonCancel.Location = new System.Drawing.Point(845, 518);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // buttonApplication
            // 
            this.buttonApplication.Location = new System.Drawing.Point(321, 518);
            this.buttonApplication.Name = "buttonApplication";
            this.buttonApplication.Size = new System.Drawing.Size(75, 23);
            this.buttonApplication.TabIndex = 3;
            this.buttonApplication.Text = "Application";
            this.buttonApplication.UseVisualStyleBackColor = true;
            this.buttonApplication.Click += new System.EventHandler(this.buttonApplication_Click);
            // 
            // buttonHttps
            // 
            this.buttonHttps.Location = new System.Drawing.Point(429, 518);
            this.buttonHttps.Name = "buttonHttps";
            this.buttonHttps.Size = new System.Drawing.Size(75, 23);
            this.buttonHttps.TabIndex = 4;
            this.buttonHttps.Text = "Https";
            this.buttonHttps.UseVisualStyleBackColor = true;
            this.buttonHttps.Click += new System.EventHandler(this.buttonHttps_Click);
            // 
            // buttonUser
            // 
            this.buttonUser.Location = new System.Drawing.Point(535, 518);
            this.buttonUser.Name = "buttonUser";
            this.buttonUser.Size = new System.Drawing.Size(75, 23);
            this.buttonUser.TabIndex = 5;
            this.buttonUser.Text = "User";
            this.buttonUser.UseVisualStyleBackColor = true;
            this.buttonUser.Click += new System.EventHandler(this.buttonUser_Click);
            // 
            // trustListControl
            // 
            this.trustListControl.AutoSize = true;
            this.trustListControl.Location = new System.Drawing.Point(0, 0);
            this.trustListControl.Name = "trustListControl";
            this.trustListControl.Size = new System.Drawing.Size(943, 511);
            this.trustListControl.TabIndex = 0;
            // 
            // ManageTrustList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(942, 548);
            this.Controls.Add(this.buttonUser);
            this.Controls.Add(this.buttonHttps);
            this.Controls.Add(this.buttonApplication);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.trustListControl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "ManageTrustList";
            this.Text = "ManageTrustList";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TrustListControl trustListControl;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonApplication;
        private System.Windows.Forms.Button buttonHttps;
        private System.Windows.Forms.Button buttonUser;
    }
}
