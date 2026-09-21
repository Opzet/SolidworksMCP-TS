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
    private CheckBox humanInLoopCheckBox;
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
        btnLaunchSolidoworksAndTestConnection = new Button( );
        mainTabs = new TabControl( );
        workspaceTab = new TabPage( );
        workspaceLayout = new TableLayoutPanel( );
        chatSplit = new SplitContainer( );
        chatLog = new RichTextBox( );
        rightPanel = new TableLayoutPanel( );
        uploadButton = new Button( );
        imagePathLabel = new Label( );
        attachImageCheckBox = new CheckBox( );
        humanInLoopCheckBox = new CheckBox( );
        imagePreview = new PictureBox( );
        decodeImageButton = new Button( );
        demoButton = new Button( );
        comboBox1 = new ComboBox( );
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
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 29F));
        rootLayout.Controls.Add(headerPanel, 0, 0);
        rootLayout.Controls.Add(mainTabs, 0, 1);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Location = new Point(0, 0);
        rootLayout.Margin = new Padding(4, 5, 4, 5);
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(17, 20, 17, 20);
        rootLayout.RowCount = 2;
        rootLayout.RowStyles.Add(new RowStyle( ));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Size = new Size(1829, 1433);
        rootLayout.TabIndex = 0;
        // 
        // headerPanel
        // 
        headerPanel.BackColor = Color.FromArgb(  37,   41,   48);
        headerPanel.Controls.Add(appTitleLabel);
        headerPanel.Controls.Add(statusLabel);
        headerPanel.Controls.Add(btnLaunchSolidoworksAndTestConnection);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Location = new Point(17, 20);
        headerPanel.Margin = new Padding(0, 0, 0, 17);
        headerPanel.Name = "headerPanel";
        headerPanel.Padding = new Padding(17);
        headerPanel.Size = new Size(1795, 77);
        headerPanel.TabIndex = 0;
        // 
        // appTitleLabel
        // 
        appTitleLabel.AutoSize = true;
        appTitleLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        appTitleLabel.ForeColor = Color.Gainsboro;
        appTitleLabel.Location = new Point(17, 20);
        appTitleLabel.Margin = new Padding(4, 0, 4, 0);
        appTitleLabel.Name = "appTitleLabel";
        appTitleLabel.Size = new Size(256, 30);
        appTitleLabel.TabIndex = 0;
        appTitleLabel.Text = "SolidWorks Orchestrator";
        // 
        // statusLabel
        // 
        statusLabel.Anchor =  AnchorStyles.Top | AnchorStyles.Right;
        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.Gainsboro;
        statusLabel.Location = new Point(3052, 23);
        statusLabel.Margin = new Padding(4, 0, 4, 0);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(174, 25);
        statusLabel.TabIndex = 1;
        statusLabel.Text = "Status: disconnected";
        // 
        // btnLaunchSolidoworksAndTestConnection
        // 
        btnLaunchSolidoworksAndTestConnection.AutoSize = true;
        btnLaunchSolidoworksAndTestConnection.BackColor = Color.FromArgb(  255,   128,   128);
        btnLaunchSolidoworksAndTestConnection.FlatAppearance.BorderSize = 0;
        btnLaunchSolidoworksAndTestConnection.FlatStyle = FlatStyle.Flat;
        btnLaunchSolidoworksAndTestConnection.ForeColor = Color.White;
        btnLaunchSolidoworksAndTestConnection.Location = new Point(1487, 12);
        btnLaunchSolidoworksAndTestConnection.Margin = new Padding(0, 20, 0, 0);
        btnLaunchSolidoworksAndTestConnection.Name = "btnLaunchSolidoworksAndTestConnection";
        btnLaunchSolidoworksAndTestConnection.Padding = new Padding(17, 10, 17, 10);
        btnLaunchSolidoworksAndTestConnection.Size = new Size(303, 78);
        btnLaunchSolidoworksAndTestConnection.TabIndex = 6;
        btnLaunchSolidoworksAndTestConnection.Text = "Warm up Solidworks";
        btnLaunchSolidoworksAndTestConnection.UseVisualStyleBackColor = false;
        btnLaunchSolidoworksAndTestConnection.Click += btnLaunchSolidoworksAndTestConnection_Click;
        // 
        // mainTabs
        // 
        mainTabs.Controls.Add(workspaceTab);
        mainTabs.Controls.Add(settingsTab);
        mainTabs.Dock = DockStyle.Fill;
        mainTabs.Font = new Font("Segoe UI", 9F);
        mainTabs.Location = new Point(21, 119);
        mainTabs.Margin = new Padding(4, 5, 4, 5);
        mainTabs.Name = "mainTabs";
        mainTabs.Padding = new Point(14, 6);
        mainTabs.SelectedIndex = 0;
        mainTabs.Size = new Size(1787, 1289);
        mainTabs.TabIndex = 1;
        // 
        // workspaceTab
        // 
        workspaceTab.BackColor = Color.FromArgb(  27,   30,   36);
        workspaceTab.Controls.Add(workspaceLayout);
        workspaceTab.Location = new Point(4, 40);
        workspaceTab.Margin = new Padding(4, 5, 4, 5);
        workspaceTab.Name = "workspaceTab";
        workspaceTab.Padding = new Padding(11, 13, 11, 13);
        workspaceTab.Size = new Size(1779, 1245);
        workspaceTab.TabIndex = 0;
        workspaceTab.Text = "Workspace";
        // 
        // workspaceLayout
        // 
        workspaceLayout.BackColor = Color.FromArgb(  27,   30,   36);
        workspaceLayout.ColumnCount = 1;
        workspaceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1751F));
        workspaceLayout.Controls.Add(chatSplit, 0, 0);
        workspaceLayout.Controls.Add(inputPanel, 0, 1);
        workspaceLayout.Dock = DockStyle.Fill;
        workspaceLayout.Location = new Point(11, 13);
        workspaceLayout.Margin = new Padding(4, 5, 4, 5);
        workspaceLayout.Name = "workspaceLayout";
        workspaceLayout.RowCount = 2;
        workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        workspaceLayout.RowStyles.Add(new RowStyle( ));
        workspaceLayout.Size = new Size(1757, 1219);
        workspaceLayout.TabIndex = 0;
        // 
        // chatSplit
        // 
        chatSplit.BackColor = Color.FromArgb(  27,   30,   36);
        chatSplit.Dock = DockStyle.Fill;
        chatSplit.Location = new Point(4, 5);
        chatSplit.Margin = new Padding(4, 5, 4, 5);
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
        chatSplit.Size = new Size(1749, 1029);
        chatSplit.SplitterDistance = 1410;
        chatSplit.SplitterWidth = 6;
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
        chatLog.Margin = new Padding(4, 5, 4, 5);
        chatLog.Name = "chatLog";
        chatLog.ReadOnly = true;
        chatLog.Size = new Size(1410, 1029);
        chatLog.TabIndex = 0;
        chatLog.Text = "";
        // 
        // rightPanel
        // 
        rightPanel.BackColor = Color.FromArgb(  37,   41,   48);
        rightPanel.ColumnCount = 1;
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 303F));
        rightPanel.Controls.Add(uploadButton, 0, 0);
        rightPanel.Controls.Add(imagePathLabel, 0, 1);
        rightPanel.Controls.Add(attachImageCheckBox, 0, 2);
        rightPanel.Controls.Add(humanInLoopCheckBox, 0, 3);
        rightPanel.Controls.Add(imagePreview, 0, 4);
        rightPanel.Controls.Add(decodeImageButton, 0, 5);
        rightPanel.Controls.Add(demoButton, 0, 6);
        rightPanel.Controls.Add(comboBox1, 0, 7);
        rightPanel.Dock = DockStyle.Fill;
        rightPanel.Location = new Point(0, 0);
        rightPanel.Margin = new Padding(4, 5, 4, 5);
        rightPanel.Name = "rightPanel";
        rightPanel.Padding = new Padding(14, 17, 14, 17);
        rightPanel.RowCount = 8;
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 97F));
        rightPanel.RowStyles.Add(new RowStyle( ));
        rightPanel.RowStyles.Add(new RowStyle( ));
        rightPanel.RowStyles.Add(new RowStyle( ));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));
        rightPanel.Size = new Size(333, 1029);
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
        uploadButton.Location = new Point(18, 22);
        uploadButton.Margin = new Padding(4, 5, 4, 5);
        uploadButton.Name = "uploadButton";
        uploadButton.Padding = new Padding(11, 8, 11, 8);
        uploadButton.Size = new Size(297, 87);
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
        imagePathLabel.Location = new Point(18, 114);
        imagePathLabel.Margin = new Padding(4, 0, 4, 0);
        imagePathLabel.Name = "imagePathLabel";
        imagePathLabel.Size = new Size(159, 25);
        imagePathLabel.TabIndex = 1;
        imagePathLabel.Text = "No image selected";
        // 
        // attachImageCheckBox
        // 
        attachImageCheckBox.AutoSize = true;
        attachImageCheckBox.BackColor = Color.Transparent;
        attachImageCheckBox.Checked = true;
        attachImageCheckBox.CheckState = CheckState.Checked;
        attachImageCheckBox.ForeColor = Color.Gainsboro;
        attachImageCheckBox.Location = new Point(18, 144);
        attachImageCheckBox.Margin = new Padding(4, 5, 4, 5);
        attachImageCheckBox.Name = "attachImageCheckBox";
        attachImageCheckBox.Size = new Size(269, 29);
        attachImageCheckBox.TabIndex = 2;
        attachImageCheckBox.Text = "Attach image to next prompt";
        attachImageCheckBox.UseVisualStyleBackColor = false;
        // 
        // humanInLoopCheckBox
        // 
        humanInLoopCheckBox.AutoSize = true;
        humanInLoopCheckBox.BackColor = Color.Transparent;
        humanInLoopCheckBox.ForeColor = Color.Gainsboro;
        humanInLoopCheckBox.Location = new Point(18, 183);
        humanInLoopCheckBox.Margin = new Padding(4, 5, 4, 5);
        humanInLoopCheckBox.Name = "humanInLoopCheckBox";
        humanInLoopCheckBox.Size = new Size(248, 29);
        humanInLoopCheckBox.TabIndex = 3;
        humanInLoopCheckBox.Text = "Validate each step with me";
        humanInLoopCheckBox.UseVisualStyleBackColor = false;
        // 
        // imagePreview
        // 
        imagePreview.BackColor = Color.FromArgb(  19,   21,   27);
        imagePreview.BorderStyle = BorderStyle.FixedSingle;
        imagePreview.Dock = DockStyle.Fill;
        imagePreview.Location = new Point(18, 222);
        imagePreview.Margin = new Padding(4, 5, 4, 5);
        imagePreview.Name = "imagePreview";
        imagePreview.Size = new Size(297, 560);
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
        decodeImageButton.Location = new Point(14, 800);
        decodeImageButton.Margin = new Padding(0, 13, 0, 0);
        decodeImageButton.Name = "decodeImageButton";
        decodeImageButton.Padding = new Padding(11, 10, 11, 10);
        decodeImageButton.Size = new Size(305, 62);
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
        demoButton.Location = new Point(14, 875);
        demoButton.Margin = new Padding(0, 13, 0, 0);
        demoButton.Name = "demoButton";
        demoButton.Padding = new Padding(11, 10, 11, 10);
        demoButton.Size = new Size(305, 62);
        demoButton.TabIndex = 5;
        demoButton.Text = "Demo:";
        demoButton.UseVisualStyleBackColor = false;
        // 
        // comboBox1
        // 
        comboBox1.Dock = DockStyle.Fill;
        comboBox1.FormattingEnabled = true;
        comboBox1.Location = new Point(18, 942);
        comboBox1.Margin = new Padding(4, 5, 4, 5);
        comboBox1.Name = "comboBox1";
        comboBox1.Size = new Size(297, 33);
        comboBox1.TabIndex = 6;
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
        inputPanel.Location = new Point(0, 1056);
        inputPanel.Margin = new Padding(0, 17, 0, 0);
        inputPanel.Name = "inputPanel";
        inputPanel.Padding = new Padding(14, 17, 14, 17);
        inputPanel.RowCount = 1;
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
        inputPanel.Size = new Size(1757, 163);
        inputPanel.TabIndex = 1;
        // 
        // inputBox
        // 
        inputBox.BackColor = Color.FromArgb(  19,   21,   27);
        inputBox.BorderStyle = BorderStyle.FixedSingle;
        inputBox.Dock = DockStyle.Fill;
        inputBox.Font = new Font("Segoe UI", 10F);
        inputBox.ForeColor = Color.Gainsboro;
        inputBox.Location = new Point(18, 22);
        inputBox.Margin = new Padding(4, 5, 4, 5);
        inputBox.Multiline = true;
        inputBox.Name = "inputBox";
        inputBox.ScrollBars = ScrollBars.Vertical;
        inputBox.Size = new Size(901, 120);
        inputBox.TabIndex = 0;
        // 
        // sendButton
        // 
        sendButton.BackColor = Color.FromArgb(  49,   120,   198);
        sendButton.Dock = DockStyle.Right;
        sendButton.FlatAppearance.BorderSize = 0;
        sendButton.FlatStyle = FlatStyle.Flat;
        sendButton.ForeColor = Color.White;
        sendButton.Location = new Point(937, 17);
        sendButton.Margin = new Padding(14, 0, 0, 0);
        sendButton.Name = "sendButton";
        sendButton.Padding = new Padding(11, 10, 11, 10);
        sendButton.Size = new Size(171, 130);
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
        stopButton.Location = new Point(1122, 17);
        stopButton.Margin = new Padding(14, 0, 0, 0);
        stopButton.Name = "stopButton";
        stopButton.Padding = new Padding(11, 10, 11, 10);
        stopButton.Size = new Size(171, 130);
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
        approvePlanButton.Location = new Point(1307, 17);
        approvePlanButton.Margin = new Padding(14, 0, 0, 0);
        approvePlanButton.Name = "approvePlanButton";
        approvePlanButton.Padding = new Padding(11, 10, 11, 10);
        approvePlanButton.Size = new Size(136, 130);
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
        rejectPlanButton.Location = new Point(1457, 17);
        rejectPlanButton.Margin = new Padding(14, 0, 0, 0);
        rejectPlanButton.Name = "rejectPlanButton";
        rejectPlanButton.Padding = new Padding(11, 10, 11, 10);
        rejectPlanButton.Size = new Size(136, 130);
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
        executePlanButton.Location = new Point(1607, 17);
        executePlanButton.Margin = new Padding(14, 0, 0, 0);
        executePlanButton.Name = "executePlanButton";
        executePlanButton.Padding = new Padding(11, 10, 11, 10);
        executePlanButton.Size = new Size(136, 130);
        executePlanButton.TabIndex = 5;
        executePlanButton.Text = "Execute";
        executePlanButton.UseVisualStyleBackColor = false;
        executePlanButton.Click += ExecutePlanButton_Click;
        // 
        // settingsTab
        // 
        settingsTab.BackColor = Color.FromArgb(  27,   30,   36);
        settingsTab.Controls.Add(settingsLayout);
        settingsTab.Location = new Point(4, 40);
        settingsTab.Margin = new Padding(4, 5, 4, 5);
        settingsTab.Name = "settingsTab";
        settingsTab.Padding = new Padding(23, 27, 23, 27);
        settingsTab.Size = new Size(1779, 1245);
        settingsTab.TabIndex = 1;
        settingsTab.Text = "Settings";
        // 
        // settingsLayout
        // 
        settingsLayout.AutoSize = true;
        settingsLayout.BackColor = Color.FromArgb(  37,   41,   48);
        settingsLayout.ColumnCount = 2;
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 271F));
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        settingsLayout.Controls.Add(modelBox, 1, 1);
        settingsLayout.Controls.Add(mcpArgsBox, 1, 2);
        settingsLayout.Controls.Add(settingsHint, 0, 3);
        settingsLayout.Controls.Add(ollamaUrlBox, 1, 0);
        settingsLayout.Controls.Add(connectButton, 0, 0);
        settingsLayout.Dock = DockStyle.Top;
        settingsLayout.Location = new Point(23, 27);
        settingsLayout.Margin = new Padding(4, 5, 4, 5);
        settingsLayout.Name = "settingsLayout";
        settingsLayout.Padding = new Padding(23, 27, 23, 27);
        settingsLayout.RowCount = 5;
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.RowStyles.Add(new RowStyle( ));
        settingsLayout.Size = new Size(1733, 306);
        settingsLayout.TabIndex = 0;
        // 
        // modelBox
        // 
        modelBox.Dock = DockStyle.Top;
        modelBox.Location = new Point(298, 177);
        modelBox.Margin = new Padding(4, 5, 4, 5);
        modelBox.Name = "modelBox";
        modelBox.Size = new Size(1408, 31);
        modelBox.TabIndex = 1;
        modelBox.Text = "qwen2.5-coder:14b";
        // 
        // mcpArgsBox
        // 
        mcpArgsBox.Dock = DockStyle.Top;
        mcpArgsBox.Location = new Point(298, 218);
        mcpArgsBox.Margin = new Padding(4, 5, 4, 5);
        mcpArgsBox.Name = "mcpArgsBox";
        mcpArgsBox.Size = new Size(1408, 31);
        mcpArgsBox.TabIndex = 2;
        // 
        // settingsHint
        // 
        settingsHint.AutoSize = true;
        settingsHint.ForeColor = Color.Gainsboro;
        settingsHint.Location = new Point(27, 254);
        settingsHint.Margin = new Padding(4, 0, 4, 0);
        settingsHint.Name = "settingsHint";
        settingsHint.Size = new Size(0, 25);
        settingsHint.TabIndex = 3;
        // 
        // ollamaUrlBox
        // 
        ollamaUrlBox.Dock = DockStyle.Top;
        ollamaUrlBox.Location = new Point(298, 32);
        ollamaUrlBox.Margin = new Padding(4, 5, 4, 5);
        ollamaUrlBox.Name = "ollamaUrlBox";
        ollamaUrlBox.Size = new Size(1408, 31);
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
        connectButton.Location = new Point(23, 47);
        connectButton.Margin = new Padding(0, 20, 0, 0);
        connectButton.Name = "connectButton";
        connectButton.Padding = new Padding(17, 10, 17, 10);
        connectButton.Size = new Size(271, 125);
        connectButton.TabIndex = 5;
        connectButton.Text = "Connect to Ai Box and Launch Solidworks MCP Service";
        connectButton.UseVisualStyleBackColor = false;
        connectButton.Click += ConnectButton_Click;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(10F, 25F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(  27,   30,   36);
        ClientSize = new Size(1829, 1433);
        Controls.Add(rootLayout);
        Margin = new Padding(4, 5, 4, 5);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SolidWorks Chat Client (Ai Box + MCP)";
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
    private ComboBox comboBox1;
}
