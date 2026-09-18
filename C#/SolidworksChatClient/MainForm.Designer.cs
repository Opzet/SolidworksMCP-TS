#nullable disable

namespace SolidworksChatClient;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel rootLayout;
    private Panel headerPanel;
    private Label appTitleLabel;
    private Label statusLabel;

    private TabControl mainTabs;
    private TabPage workspaceTab;
    private TabPage settingsTab;

    private TableLayoutPanel workspaceLayout;
    private SplitContainer chatSplit;
    private TableLayoutPanel rightPanel;
    private TableLayoutPanel inputPanel;

    private RichTextBox chatLog;
    private TextBox inputBox;

    private Label imagePathLabel;
    private CheckBox attachImageCheckBox;
    private Button uploadButton;
    private Button decodeImageButton;
    private Button demoButton;
    private Button sendButton;
    private Button stopButton;
    private Button approvePlanButton;
    private Button rejectPlanButton;
    private Button executePlanButton;
    private PictureBox imagePreview;

    private TableLayoutPanel settingsLayout;
    private TextBox ollamaUrlBox;
    private TextBox modelBox;
    private TextBox mcpArgsBox;
    private Button connectButton;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        rootLayout = new TableLayoutPanel( );
        headerPanel = new Panel( );
        appTitleLabel = new Label( );
        statusLabel = new Label( );
        mainTabs = new TabControl( );
        workspaceTab = new TabPage( );
        workspaceLayout = new TableLayoutPanel( );
        chatSplit = new SplitContainer( );
        chatLog = new RichTextBox( );
        rightPanel = new TableLayoutPanel( );
        uploadButton = new Button( );
        imagePathLabel = new Label( );
        btnLaunchSolidoworksAndTestConnection = new Button( );
        attachImageCheckBox = new CheckBox( );
        imagePreview = new PictureBox( );
        decodeImageButton = new Button( );
        demoButton = new Button( );
        inputPanel = new TableLayoutPanel( );
        inputBox = new TextBox( );
        sendButton = new Button( );
        stopButton = new Button( );
        approvePlanButton = new Button( );
        rejectPlanButton = new Button( );
        executePlanButton = new Button( );
        settingsTab = new TabPage( );
        settingsLayout = new TableLayoutPanel( );
        modelBox = new TextBox( );
        mcpArgsBox = new TextBox( );
        settingsHint = new Label( );
        ollamaUrlBox = new TextBox( );
        connectButton = new Button( );
        rootLayout.SuspendLayout( );
        headerPanel.SuspendLayout( );
        mainTabs.SuspendLayout( );
        workspaceTab.SuspendLayout( );
        workspaceLayout.SuspendLayout( );
        ((System.ComponentModel.ISupportInitialize) chatSplit).BeginInit( );
        chatSplit.Panel1.SuspendLayout( );
        chatSplit.Panel2.SuspendLayout( );
        chatSplit.SuspendLayout( );
        rightPanel.SuspendLayout( );
        ((System.ComponentModel.ISupportInitialize) imagePreview).BeginInit( );
        inputPanel.SuspendLayout( );
        settingsTab.SuspendLayout( );
        settingsLayout.SuspendLayout( );
        SuspendLayout( );
        // 
        // rootLayout
        // 
        rootLayout.BackColor = Color.FromArgb(  27,   30,   36);
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        rootLayout.Controls.Add(headerPanel, 0, 0);
        rootLayout.Controls.Add(mainTabs, 0, 1);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Location = new Point(0, 0);
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(12);
        rootLayout.RowCount = 2;
        rootLayout.RowStyles.Add(new RowStyle( ));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Size = new Size(1280, 860);
        rootLayout.TabIndex = 0;
        // 
        // headerPanel
        // 
        headerPanel.BackColor = Color.FromArgb(  37,   41,   48);
        headerPanel.Controls.Add(appTitleLabel);
        headerPanel.Controls.Add(statusLabel);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Location = new Point(12, 12);
        headerPanel.Margin = new Padding(0, 0, 0, 10);
        headerPanel.Name = "headerPanel";
        headerPanel.Padding = new Padding(12, 10, 12, 10);
        headerPanel.Size = new Size(1256, 46);
        headerPanel.TabIndex = 0;
        // 
        // appTitleLabel
        // 
        appTitleLabel.AutoSize = true;
        appTitleLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        appTitleLabel.ForeColor = Color.Gainsboro;
        appTitleLabel.Location = new Point(12, 12);
        appTitleLabel.Name = "appTitleLabel";
        appTitleLabel.Size = new Size(176, 20);
        appTitleLabel.TabIndex = 0;
        appTitleLabel.Text = "SolidWorks Orchestrator";
        // 
        // statusLabel
        // 
        statusLabel.Anchor =  AnchorStyles.Top | AnchorStyles.Right;
        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.Gainsboro;
        statusLabel.Location = new Point(2136, 14);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(116, 15);
        statusLabel.TabIndex = 1;
        statusLabel.Text = "Status: disconnected";
        // 
        // mainTabs
        // 
        mainTabs.Controls.Add(workspaceTab);
        mainTabs.Controls.Add(settingsTab);
        mainTabs.Dock = DockStyle.Fill;
        mainTabs.Font = new Font("Segoe UI", 9F);
        mainTabs.Location = new Point(15, 71);
        mainTabs.Name = "mainTabs";
        mainTabs.Padding = new Point(14, 6);
        mainTabs.SelectedIndex = 0;
        mainTabs.Size = new Size(1250, 774);
        mainTabs.TabIndex = 1;
        // 
        // workspaceTab
        // 
        workspaceTab.BackColor = Color.FromArgb(  27,   30,   36);
        workspaceTab.Controls.Add(workspaceLayout);
        workspaceTab.Location = new Point(4, 30);
        workspaceTab.Name = "workspaceTab";
        workspaceTab.Padding = new Padding(8);
        workspaceTab.Size = new Size(1242, 740);
        workspaceTab.TabIndex = 0;
        workspaceTab.Text = "Workspace";
        // 
        // workspaceLayout
        // 
        workspaceLayout.BackColor = Color.FromArgb(  27,   30,   36);
        workspaceLayout.ColumnCount = 1;
        workspaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        workspaceLayout.Controls.Add(chatSplit, 0, 0);
        workspaceLayout.Controls.Add(inputPanel, 0, 1);
        workspaceLayout.Dock = DockStyle.Fill;
        workspaceLayout.Location = new Point(8, 8);
        workspaceLayout.Name = "workspaceLayout";
        workspaceLayout.RowCount = 2;
        workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        workspaceLayout.RowStyles.Add(new RowStyle( ));
        workspaceLayout.Size = new Size(1226, 724);
        workspaceLayout.TabIndex = 0;
        // 
        // chatSplit
        // 
        chatSplit.BackColor = Color.FromArgb(  27,   30,   36);
        chatSplit.Dock = DockStyle.Fill;
        chatSplit.Location = new Point(3, 3);
        chatSplit.Name = "chatSplit";
        // 
        // chatSplit.Panel1
        // 
        chatSplit.Panel1.Controls.Add(chatLog);
        chatSplit.Panel1MinSize = 420;
        // 
        // chatSplit.Panel2
        // 
        chatSplit.Panel2.Controls.Add(rightPanel);
        chatSplit.Panel2MinSize = 220;
        chatSplit.Size = new Size(1220, 610);
        chatSplit.SplitterDistance = 984;
        chatSplit.TabIndex = 0;
        // 
        // chatLog
        // 
        chatLog.BackColor = Color.FromArgb(  19,   21,   27);
        chatLog.BorderStyle = BorderStyle.None;
        chatLog.Dock = DockStyle.Fill;
        chatLog.Font = new Font("Consolas", 10.5F);
        chatLog.ForeColor = Color.Gainsboro;
        chatLog.Location = new Point(0, 0);
        chatLog.Name = "chatLog";
        chatLog.ReadOnly = true;
        chatLog.Size = new Size(984, 610);
        chatLog.TabIndex = 0;
        chatLog.Text = "";
        // 
        // rightPanel
        // 
        rightPanel.BackColor = Color.FromArgb(  37,   41,   48);
        rightPanel.ColumnCount = 1;
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        rightPanel.Controls.Add(uploadButton, 0, 0);
        rightPanel.Controls.Add(imagePathLabel, 0, 1);
        rightPanel.Controls.Add(btnLaunchSolidoworksAndTestConnection, 0, 6);
        rightPanel.Controls.Add(attachImageCheckBox, 0, 2);
        rightPanel.Controls.Add(imagePreview, 0, 3);
        rightPanel.Controls.Add(decodeImageButton, 0, 4);
        rightPanel.Controls.Add(demoButton, 0, 5);
        rightPanel.Dock = DockStyle.Fill;
        rightPanel.Location = new Point(0, 0);
        rightPanel.Name = "rightPanel";
        rightPanel.Padding = new Padding(10);
        rightPanel.RowCount = 7;
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        rightPanel.RowStyles.Add(new RowStyle( ));
        rightPanel.RowStyles.Add(new RowStyle( ));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        rightPanel.Size = new Size(232, 610);
        rightPanel.TabIndex = 0;
        // 
        // uploadButton
        // 
        uploadButton.AutoSize = true;
        uploadButton.BackColor = Color.FromArgb(  84,   95,   112);
        uploadButton.Dock = DockStyle.Fill;
        uploadButton.FlatAppearance.BorderSize = 0;
        uploadButton.FlatStyle = FlatStyle.Flat;
        uploadButton.ForeColor = Color.White;
        uploadButton.Location = new Point(13, 13);
        uploadButton.Name = "uploadButton";
        uploadButton.Padding = new Padding(8, 5, 8, 5);
        uploadButton.Size = new Size(206, 52);
        uploadButton.TabIndex = 0;
        uploadButton.Text = "Upload Image";
        uploadButton.UseVisualStyleBackColor = false;
        uploadButton.Click += UploadButton_Click;
        // 
        // imagePathLabel
        // 
        imagePathLabel.AutoEllipsis = true;
        imagePathLabel.AutoSize = true;
        imagePathLabel.ForeColor = Color.Gainsboro;
        imagePathLabel.Location = new Point(13, 68);
        imagePathLabel.Name = "imagePathLabel";
        imagePathLabel.Size = new Size(105, 15);
        imagePathLabel.TabIndex = 1;
        imagePathLabel.Text = "No image selected";
        // 
        // btnLaunchSolidoworksAndTestConnection
        // 
        btnLaunchSolidoworksAndTestConnection.AutoSize = true;
        btnLaunchSolidoworksAndTestConnection.BackColor = Color.FromArgb(  255,   128,   128);
        btnLaunchSolidoworksAndTestConnection.FlatAppearance.BorderSize = 0;
        btnLaunchSolidoworksAndTestConnection.FlatStyle = FlatStyle.Flat;
        btnLaunchSolidoworksAndTestConnection.ForeColor = Color.White;
        btnLaunchSolidoworksAndTestConnection.Location = new Point(10, 567);
        btnLaunchSolidoworksAndTestConnection.Margin = new Padding(0, 12, 0, 0);
        btnLaunchSolidoworksAndTestConnection.Name = "btnLaunchSolidoworksAndTestConnection";
        btnLaunchSolidoworksAndTestConnection.Padding = new Padding(12, 6, 12, 6);
        btnLaunchSolidoworksAndTestConnection.Size = new Size(212, 33);
        btnLaunchSolidoworksAndTestConnection.TabIndex = 6;
        btnLaunchSolidoworksAndTestConnection.Text = "Warm up Solidworks";
        btnLaunchSolidoworksAndTestConnection.UseVisualStyleBackColor = false;
        btnLaunchSolidoworksAndTestConnection.Click += btnLaunchSolidoworksAndTestConnection_Click;
        // 
        // attachImageCheckBox
        // 
        attachImageCheckBox.AutoSize = true;
        attachImageCheckBox.BackColor = Color.Transparent;
        attachImageCheckBox.Checked = true;
        attachImageCheckBox.CheckState = CheckState.Checked;
        attachImageCheckBox.ForeColor = Color.Gainsboro;
        attachImageCheckBox.Location = new Point(13, 86);
        attachImageCheckBox.Name = "attachImageCheckBox";
        attachImageCheckBox.Size = new Size(179, 19);
        attachImageCheckBox.TabIndex = 2;
        attachImageCheckBox.Text = "Attach image to next prompt";
        attachImageCheckBox.UseVisualStyleBackColor = false;
        // 
        // imagePreview
        // 
        imagePreview.BackColor = Color.FromArgb(  19,   21,   27);
        imagePreview.BorderStyle = BorderStyle.FixedSingle;
        imagePreview.Dock = DockStyle.Fill;
        imagePreview.Location = new Point(13, 111);
        imagePreview.Name = "imagePreview";
        imagePreview.Size = new Size(206, 351);
        imagePreview.SizeMode = PictureBoxSizeMode.Zoom;
        imagePreview.TabIndex = 3;
        imagePreview.TabStop = false;
        // 
        // decodeImageButton
        // 
        decodeImageButton.BackColor = Color.FromArgb(  49,   120,   198);
        decodeImageButton.Dock = DockStyle.Fill;
        decodeImageButton.FlatAppearance.BorderSize = 0;
        decodeImageButton.FlatStyle = FlatStyle.Flat;
        decodeImageButton.ForeColor = Color.White;
        decodeImageButton.Location = new Point(10, 473);
        decodeImageButton.Margin = new Padding(0, 8, 0, 0);
        decodeImageButton.Name = "decodeImageButton";
        decodeImageButton.Padding = new Padding(8, 6, 8, 6);
        decodeImageButton.Size = new Size(212, 37);
        decodeImageButton.TabIndex = 4;
        decodeImageButton.Text = "Decode + Plan CAD Steps";
        decodeImageButton.UseVisualStyleBackColor = false;
        decodeImageButton.Click += DecodeImageButton_Click;
        // 
        // demoButton
        // 
        demoButton.BackColor = Color.FromArgb(  48,   160,   94);
        demoButton.Dock = DockStyle.Fill;
        demoButton.FlatAppearance.BorderSize = 0;
        demoButton.FlatStyle = FlatStyle.Flat;
        demoButton.ForeColor = Color.White;
        demoButton.Location = new Point(10, 518);
        demoButton.Margin = new Padding(0, 8, 0, 0);
        demoButton.Name = "demoButton";
        demoButton.Padding = new Padding(8, 6, 8, 6);
        demoButton.Size = new Size(212, 37);
        demoButton.TabIndex = 5;
        demoButton.Text = "Demo: 4-Bar Linkage GA + Parts";
        demoButton.UseVisualStyleBackColor = false;
        demoButton.Click += DemoButton_Click;
        // 
        // inputPanel
        // 
        inputPanel.BackColor = Color.FromArgb(  37,   41,   48);
        inputPanel.ColumnCount = 6;
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        inputPanel.ColumnStyles.Add(new ColumnStyle( ));
        inputPanel.ColumnStyles.Add(new ColumnStyle( ));
        inputPanel.ColumnStyles.Add(new ColumnStyle( ));
        inputPanel.ColumnStyles.Add(new ColumnStyle( ));
        inputPanel.ColumnStyles.Add(new ColumnStyle( ));
        inputPanel.Controls.Add(inputBox, 0, 0);
        inputPanel.Controls.Add(sendButton, 1, 0);
        inputPanel.Controls.Add(stopButton, 2, 0);
        inputPanel.Controls.Add(approvePlanButton, 3, 0);
        inputPanel.Controls.Add(rejectPlanButton, 4, 0);
        inputPanel.Controls.Add(executePlanButton, 5, 0);
        inputPanel.Dock = DockStyle.Bottom;
        inputPanel.Location = new Point(0, 626);
        inputPanel.Margin = new Padding(0, 10, 0, 0);
        inputPanel.Name = "inputPanel";
        inputPanel.Padding = new Padding(10);
        inputPanel.RowCount = 1;
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        inputPanel.Size = new Size(1226, 98);
        inputPanel.TabIndex = 1;
        // 
        // inputBox
        // 
        inputBox.BackColor = Color.FromArgb(  19,   21,   27);
        inputBox.BorderStyle = BorderStyle.FixedSingle;
        inputBox.Dock = DockStyle.Fill;
        inputBox.Font = new Font("Segoe UI", 10F);
        inputBox.ForeColor = Color.Gainsboro;
        inputBox.Location = new Point(13, 13);
        inputBox.Multiline = true;
        inputBox.Name = "inputBox";
        inputBox.ScrollBars = ScrollBars.Vertical;
        inputBox.Size = new Size(625, 72);
        inputBox.TabIndex = 0;
        // 
        // sendButton
        // 
        sendButton.BackColor = Color.FromArgb(  49,   120,   198);
        sendButton.Dock = DockStyle.Right;
        sendButton.FlatAppearance.BorderSize = 0;
        sendButton.FlatStyle = FlatStyle.Flat;
        sendButton.ForeColor = Color.White;
        sendButton.Location = new Point(651, 10);
        sendButton.Margin = new Padding(10, 0, 0, 0);
        sendButton.Name = "sendButton";
        sendButton.Padding = new Padding(8, 6, 8, 6);
        sendButton.Size = new Size(120, 78);
        sendButton.TabIndex = 1;
        sendButton.Text = "Send";
        sendButton.UseVisualStyleBackColor = false;
        sendButton.Click += SendButton_Click;
        // 
        // stopButton
        // 
        stopButton.BackColor = Color.FromArgb(  172,   61,   61);
        stopButton.Dock = DockStyle.Right;
        stopButton.Enabled = false;
        stopButton.FlatAppearance.BorderSize = 0;
        stopButton.FlatStyle = FlatStyle.Flat;
        stopButton.ForeColor = Color.White;
        stopButton.Location = new Point(781, 10);
        stopButton.Margin = new Padding(10, 0, 0, 0);
        stopButton.Name = "stopButton";
        stopButton.Padding = new Padding(8, 6, 8, 6);
        stopButton.Size = new Size(120, 78);
        stopButton.TabIndex = 2;
        stopButton.Text = "Stop";
        stopButton.UseVisualStyleBackColor = false;
        stopButton.Click += StopButton_Click;
        // 
        // approvePlanButton
        // 
        approvePlanButton.BackColor = Color.FromArgb(  73,   138,   86);
        approvePlanButton.Dock = DockStyle.Right;
        approvePlanButton.Enabled = false;
        approvePlanButton.FlatAppearance.BorderSize = 0;
        approvePlanButton.FlatStyle = FlatStyle.Flat;
        approvePlanButton.ForeColor = Color.White;
        approvePlanButton.Location = new Point(911, 10);
        approvePlanButton.Margin = new Padding(10, 0, 0, 0);
        approvePlanButton.Name = "approvePlanButton";
        approvePlanButton.Padding = new Padding(8, 6, 8, 6);
        approvePlanButton.Size = new Size(95, 78);
        approvePlanButton.TabIndex = 3;
        approvePlanButton.Text = "Approve";
        approvePlanButton.UseVisualStyleBackColor = false;
        approvePlanButton.Click += ApprovePlanButton_Click;
        // 
        // rejectPlanButton
        // 
        rejectPlanButton.BackColor = Color.FromArgb(  120,   96,   72);
        rejectPlanButton.Dock = DockStyle.Right;
        rejectPlanButton.Enabled = false;
        rejectPlanButton.FlatAppearance.BorderSize = 0;
        rejectPlanButton.FlatStyle = FlatStyle.Flat;
        rejectPlanButton.ForeColor = Color.White;
        rejectPlanButton.Location = new Point(1016, 10);
        rejectPlanButton.Margin = new Padding(10, 0, 0, 0);
        rejectPlanButton.Name = "rejectPlanButton";
        rejectPlanButton.Padding = new Padding(8, 6, 8, 6);
        rejectPlanButton.Size = new Size(95, 78);
        rejectPlanButton.TabIndex = 4;
        rejectPlanButton.Text = "Reject";
        rejectPlanButton.UseVisualStyleBackColor = false;
        rejectPlanButton.Click += RejectPlanButton_Click;
        // 
        // executePlanButton
        // 
        executePlanButton.BackColor = Color.FromArgb(  49,   120,   198);
        executePlanButton.Dock = DockStyle.Right;
        executePlanButton.Enabled = false;
        executePlanButton.FlatAppearance.BorderSize = 0;
        executePlanButton.FlatStyle = FlatStyle.Flat;
        executePlanButton.ForeColor = Color.White;
        executePlanButton.Location = new Point(1121, 10);
        executePlanButton.Margin = new Padding(10, 0, 0, 0);
        executePlanButton.Name = "executePlanButton";
        executePlanButton.Padding = new Padding(8, 6, 8, 6);
        executePlanButton.Size = new Size(95, 78);
        executePlanButton.TabIndex = 5;
        executePlanButton.Text = "Execute";
        executePlanButton.UseVisualStyleBackColor = false;
        executePlanButton.Click += ExecutePlanButton_Click;
        // 
        // settingsTab
        // 
        settingsTab.BackColor = Color.FromArgb(  27,   30,   36);
        settingsTab.Controls.Add(settingsLayout);
        settingsTab.Location = new Point(4, 30);
        settingsTab.Name = "settingsTab";
        settingsTab.Padding = new Padding(16);
        settingsTab.Size = new Size(1242, 740);
        settingsTab.TabIndex = 1;
        settingsTab.Text = "Settings";
        // 
        // settingsLayout
        // 
        settingsLayout.AutoSize = true;
        settingsLayout.BackColor = Color.FromArgb(  37,   41,   48);
        settingsLayout.ColumnCount = 2;
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        settingsLayout.Controls.Add(modelBox, 1, 1);
        settingsLayout.Controls.Add(mcpArgsBox, 1, 2);
        settingsLayout.Controls.Add(settingsHint, 0, 3);
        settingsLayout.Controls.Add(ollamaUrlBox, 1, 0);
        settingsLayout.Controls.Add(connectButton, 0, 0);
        settingsLayout.Dock = DockStyle.Top;
        settingsLayout.Location = new Point(16, 16);
        settingsLayout.Name = "settingsLayout";
        settingsLayout.Padding = new Padding(16);
        settingsLayout.RowCount = 5;
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.Size = new Size(1210, 192);
        settingsLayout.TabIndex = 0;
        // 
        // modelBox
        // 
        modelBox.Dock = DockStyle.Top;
        modelBox.Location = new Point(209, 106);
        modelBox.Name = "modelBox";
        modelBox.Size = new Size(982, 23);
        modelBox.TabIndex = 1;
        modelBox.Text = "qwen2.5-coder:14b";
        // 
        // mcpArgsBox
        // 
        mcpArgsBox.Dock = DockStyle.Top;
        mcpArgsBox.Location = new Point(209, 135);
        mcpArgsBox.Name = "mcpArgsBox";
        mcpArgsBox.Size = new Size(982, 23);
        mcpArgsBox.TabIndex = 2;
        // 
        // settingsHint
        // 
        settingsHint.AutoSize = true;
        settingsHint.ForeColor = Color.Gainsboro;
        settingsHint.Location = new Point(19, 161);
        settingsHint.Name = "settingsHint";
        settingsHint.Size = new Size(0, 15);
        settingsHint.TabIndex = 3;
        // 
        // ollamaUrlBox
        // 
        ollamaUrlBox.Dock = DockStyle.Top;
        ollamaUrlBox.Location = new Point(209, 19);
        ollamaUrlBox.Name = "ollamaUrlBox";
        ollamaUrlBox.Size = new Size(982, 23);
        ollamaUrlBox.TabIndex = 0;
        ollamaUrlBox.Text = "http://aibox:11434";
        // 
        // connectButton
        // 
        connectButton.AutoSize = true;
        connectButton.BackColor = Color.Green;
        connectButton.FlatAppearance.BorderSize = 0;
        connectButton.FlatStyle = FlatStyle.Flat;
        connectButton.ForeColor = Color.White;
        connectButton.Location = new Point(16, 28);
        connectButton.Margin = new Padding(0, 12, 0, 0);
        connectButton.Name = "connectButton";
        connectButton.Padding = new Padding(12, 6, 12, 6);
        connectButton.Size = new Size(190, 75);
        connectButton.TabIndex = 5;
        connectButton.Text = "Connect to LLM and Launch Solidworks MCP Service";
        connectButton.UseVisualStyleBackColor = false;
        connectButton.Click += ConnectButton_Click;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(  27,   30,   36);
        ClientSize = new Size(1280, 860);
        Controls.Add(rootLayout);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SolidWorks Chat Client (Ollama + MCP)";
        Load += MainForm_Load;
        rootLayout.ResumeLayout(false);
        headerPanel.ResumeLayout(false);
        headerPanel.PerformLayout( );
        mainTabs.ResumeLayout(false);
        workspaceTab.ResumeLayout(false);
        workspaceLayout.ResumeLayout(false);
        chatSplit.Panel1.ResumeLayout(false);
        chatSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize) chatSplit).EndInit( );
        chatSplit.ResumeLayout(false);
        rightPanel.ResumeLayout(false);
        rightPanel.PerformLayout( );
        ((System.ComponentModel.ISupportInitialize) imagePreview).EndInit( );
        inputPanel.ResumeLayout(false);
        inputPanel.PerformLayout( );
        settingsTab.ResumeLayout(false);
        settingsTab.PerformLayout( );
        settingsLayout.ResumeLayout(false);
        settingsLayout.PerformLayout( );
        ResumeLayout(false);
    }

    private static Label CreateHeaderLabel(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = Color.Gainsboro,
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 6),
        };
    }

    private static void ApplyDarkTextBoxStyle(TextBox textBox)
    {
        textBox.BackColor = Color.FromArgb(19, 21, 27);
        textBox.ForeColor = Color.Gainsboro;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Margin = new Padding(0, 4, 0, 4);
    }

    private Label settingsHint;
    private Button btnLaunchSolidoworksAndTestConnection;
}
