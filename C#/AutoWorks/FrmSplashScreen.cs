using System;
using System.Windows.Forms;

namespace AutoWorks
{
    public partial class FrmSplashScreen : Form
    {
        public FrmSplashScreen()
        {
            InitializeComponent();
        }

        private void FrmSplashScreen_Load(object sender, EventArgs e)
        {
            // Update VErsion
            lblVersion.Text = $"Version: {Application.ProductVersion}";

            // Center the form on screen
            this.CenterToScreen();
        }

        public void UpdateStatus(string status)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action(() => lblStatus.Text = status));
            }
            else
            {
                lblStatus.Text = status;
            }
            Application.DoEvents();
        }
    }
}
