namespace AutoWorks
{
    partial class FrmSplashScreen
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmSplashScreen));
            lblStatus = new Label( );
            progressBar = new ProgressBar( );
            panelHeader = new Panel( );
            panelFooter = new Panel( );
            lblVersion = new Label( );
            pictureBox1 = new PictureBox( );
            panelFooter.SuspendLayout( );
            ((System.ComponentModel.ISupportInitialize) pictureBox1).BeginInit( );
            SuspendLayout( );
            // 
            // lblStatus
            // 
            lblStatus.BackColor = Color.FromArgb(  255,   128,   0);
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point,  0);
            lblStatus.ForeColor = Color.White;
            lblStatus.Location = new Point(0, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Padding = new Padding(0, 5, 0, 0);
            lblStatus.Size = new Size(600, 110);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Initialising...";
            lblStatus.TextAlign = ContentAlignment.TopCenter;
            // 
            // progressBar
            // 
            progressBar.Dock = DockStyle.Bottom;
            progressBar.Location = new Point(0, 110);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(600, 12);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TabIndex = 3;
            // 
            // panelHeader
            // 
            panelHeader.BackColor = Color.FromArgb(  255,   128,   0);
            panelHeader.Dock = DockStyle.Top;
            panelHeader.Location = new Point(0, 0);
            panelHeader.Name = "panelHeader";
            panelHeader.Padding = new Padding(0, 20, 0, 10);
            panelHeader.Size = new Size(600, 180);
            panelHeader.TabIndex = 4;
            // 
            // panelFooter
            // 
            panelFooter.BackColor = Color.FromArgb(  0,   102,   204);
            panelFooter.Controls.Add(lblStatus);
            panelFooter.Controls.Add(progressBar);
            panelFooter.Controls.Add(lblVersion);
            panelFooter.Dock = DockStyle.Bottom;
            panelFooter.Location = new Point(0, 320);
            panelFooter.Name = "panelFooter";
            panelFooter.Size = new Size(600, 149);
            panelFooter.TabIndex = 5;
            // 
            // lblVersion
            // 
            lblVersion.BackColor = Color.FromArgb(  255,   128,   0);
            lblVersion.Dock = DockStyle.Bottom;
            lblVersion.Font = new Font("Segoe UI", 8F);
            lblVersion.ForeColor = Color.LightGray;
            lblVersion.Location = new Point(0, 122);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(600, 27);
            lblVersion.TabIndex = 4;
            lblVersion.Text = "...";
            lblVersion.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // pictureBox1
            // 
            pictureBox1.BackgroundImage = (Image) resources.GetObject("pictureBox1.BackgroundImage");
            pictureBox1.BackgroundImageLayout = ImageLayout.Stretch;
            pictureBox1.Location = new Point(149, 57);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(319, 227);
            pictureBox1.TabIndex = 6;
            pictureBox1.TabStop = false;
            // 
            // FrmSplashScreen
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(600, 469);
            Controls.Add(pictureBox1);
            Controls.Add(panelFooter);
            Controls.Add(panelHeader);
            FormBorderStyle = FormBorderStyle.None;
            Name = "FrmSplashScreen";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Camco Training Manager";
            Load += FrmSplashScreen_Load;
            panelFooter.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize) pictureBox1).EndInit( );
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Panel panelFooter;
        private System.Windows.Forms.Label lblVersion;
        private PictureBox pictureBox1;
    }
}
