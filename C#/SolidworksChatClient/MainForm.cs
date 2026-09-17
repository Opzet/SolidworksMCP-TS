namespace SolidworksChatClient;

using SolidworksChatClient.Runtime;
using System.Diagnostics;
using System.Text;

public partial class MainForm : Form
{
    private ChatRuntime? runtime;
    private string? selectedImagePath;
    private bool connecting;
    private int planWaitStart = -1;
    private int planWaitLength;

    public MainForm()
    {
        InitializeComponent( );
        Resize += MainForm_Resize;
        inputBox.KeyDown += InputBox_KeyDown;
        inputBox.PlaceholderText = "Describe the SolidWorks task, then press Enter to send...";

        MainForm_Resize(this, EventArgs.Empty);
        AppendLog("system", "Open Settings, connect, and the client will validate Ollama + MCP before chat starts.");
        AppendLog("system", "Tip: Enter sends message, Shift+Enter adds a new line.");
    }

    private async void ConnectButton_Click(object? sender, EventArgs e) => await ConnectAsync( ).ConfigureAwait(true);

    private async void SendButton_Click(object? sender, EventArgs e) => await SendCurrentInputAsync( ).ConfigureAwait(true);

    private async void DecodeImageButton_Click(object? sender, EventArgs e) => await SendImageDecodePromptAsync( ).ConfigureAwait(true);

    private async void DemoButton_Click(object? sender, EventArgs e) => await SendFourBarDemoPromptAsync( ).ConfigureAwait(true);

    private async void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter || e.Shift)
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;

        try
        {
            await SendCurrentInputAsync( ).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("error", ex.Message);
        }
    }

    private void UploadButton_Click(object? sender, EventArgs e) => SelectImage( );


    private async Task ConnectAsync()
    {
        if (connecting)
        {
            return;
        }

        connecting = true;
        ToggleUi(false);
        statusLabel.Text = "Status: connecting...";

        try
        {
            var configuredModel = modelBox.Text.Trim( );
            var ollamaStatus = await OllamaClient.TestConnectionAsync(ollamaUrlBox.Text.Trim( ), configuredModel).ConfigureAwait(true);
            AppendLog("system", $"Ollama reachable. Discovered {ollamaStatus.AvailableModelCount} model(s).");

            if (!ollamaStatus.IsModelAvailable)
            {
                AppendLog("error", $"Configured model '{configuredModel}' is not in Ollama tags. Continue only after pull/update.");
            }

            runtime?.Dispose( );
            runtime = new ChatRuntime(new ChatSettings(
                ollamaUrlBox.Text.Trim( ),
                configuredModel,
                mcpCommandBox.Text.Trim( ),
                SplitCommandLine(mcpArgsBox.Text.Trim( ))));

            await runtime.InitializeAsync( ).ConfigureAwait(true);

            var mcpStatus = await runtime.TestMcpConnectionAsync( ).ConfigureAwait(true);
            AppendLog("system", $"MCP connected. {mcpStatus.ToolCount} tool(s) available.");
            AppendLog("plan", $"Tool catalog: {FormatToolList(mcpStatus.ToolNames)}");

            var solidWorksStatus = await runtime.WarmupSolidWorksAsync( ).ConfigureAwait(true);
            AppendLog(solidWorksStatus.Succeeded ? "system" : "error", solidWorksStatus.Message);

            statusLabel.Text = solidWorksStatus.Succeeded
                ? "Status: connected + SolidWorks ready"
                : "Status: connected (SolidWorks warm-up skipped/failed)";
        }
        catch (Exception ex)
        {
            runtime?.Dispose( );
            runtime = null;
            AppendLog("error", ex.Message);
            statusLabel.Text = "Status: connection failed";
        }
        finally
        {
            connecting = false;
            ToggleUi(true);
        }
    }

    private async Task SendCurrentInputAsync()
    {
        var text = inputBox.Text.Trim( );
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await SendPromptAsync(text).ConfigureAwait(true);
    }

    private async Task SendImageDecodePromptAsync()
    {
        if (string.IsNullOrWhiteSpace(selectedImagePath) || !File.Exists(selectedImagePath))
        {
            AppendLog("system", "Select an image first.");
            return;
        }

        const string decodePrompt = "Decode this image into engineering intent and propose exact SolidWorks build steps, dimensions, constraints, BOM candidates, and drawing views required. After analysis, use tools where possible to start model creation.";
        await SendPromptAsync(decodePrompt).ConfigureAwait(true);
    }

    private async Task SendFourBarDemoPromptAsync()
    {
        const string demoPrompt = "Create a complete 4-bar linkage demo using available MCP tools only. Steps: 1) create a new part and sketch linkage plates with holes, 2) extrude features, 3) set key dimensions, 4) rebuild model, 5) create drawing from model, 6) add front and isometric views, 7) summarize generated artifacts and remaining manual CAD steps if any.";
        await SendPromptAsync(demoPrompt).ConfigureAwait(true);
    }

    private async Task SendPromptAsync(string prompt)
    {
        if (runtime is null)
        {
            AppendLog("system", "Not connected. Open Settings tab and click Connect first.");
            return;
        }

        ToggleUi(false);
        inputBox.Text = string.Empty;
        AppendLog("you", prompt);
        BeginPlanWaitLine( );

        using var waitCounterCancellation = new CancellationTokenSource( );
        var waitCounterTask = RunPlanWaitCounterAsync(waitCounterCancellation.Token);

        try
        {
            var imageBase64 = attachImageCheckBox.Checked && !string.IsNullOrWhiteSpace(selectedImagePath) && File.Exists(selectedImagePath)
                ? Convert.ToBase64String(await File.ReadAllBytesAsync(selectedImagePath).ConfigureAwait(true))
                : null;

            var result = await runtime.SendAsync(prompt, imageBase64).ConfigureAwait(true);

            waitCounterCancellation.Cancel( );
            await waitCounterTask.ConfigureAwait(true);
            CompletePlanWaitLine( );

            foreach (var progressUpdate in result.ProgressUpdates)
            {
                AppendLog("plan", progressUpdate);
            }

            AppendLog("assistant", result.FinalResponse);

            foreach (var toolCall in result.ToolCalls)
            {
                AppendLog("tool", $"> {toolCall.Name}({toolCall.Arguments})");
                AppendLog("tool", $"< {toolCall.Result}");
            }
        }
        catch (Exception ex)
        {
            waitCounterCancellation.Cancel( );
            await waitCounterTask.ConfigureAwait(true);
            CompletePlanWaitLine( );
            AppendLog("error", ex.Message);
        }
        finally
        {
            ToggleUi(true);
            inputBox.Focus( );
        }
    }

    private async Task RunPlanWaitCounterAsync(CancellationToken cancellationToken)
    {
        var elapsedSeconds = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            elapsedSeconds++;
            UpdatePlanWaitLine(elapsedSeconds);
        }
    }

    private void BeginPlanWaitLine()
    {
        planWaitStart = -1;
        planWaitLength = 0;
        UpdatePlanWaitLine(0);
    }

    private void UpdatePlanWaitLine(int elapsedSeconds)
    {
        var entry = $"[plan] Assistant is drafting a tool plan. Please wait... {elapsedSeconds}s{Environment.NewLine}{Environment.NewLine}";
        var wasReadOnly = chatLog.ReadOnly;

        try
        {
            chatLog.ReadOnly = false;

            if (planWaitStart >= 0 && planWaitLength > 0)
            {
                chatLog.Select(planWaitStart, planWaitLength);
                chatLog.SelectedText = string.Empty;
                chatLog.SelectionStart = planWaitStart;
            }
            else
            {
                planWaitStart = chatLog.TextLength;
                chatLog.SelectionStart = planWaitStart;
            }

            chatLog.SelectionLength = 0;
            chatLog.SelectionColor = Color.PaleTurquoise;
            chatLog.SelectedText = entry;
            planWaitLength = entry.Length;
            chatLog.SelectionStart = chatLog.TextLength;
            chatLog.ScrollToCaret( );
        }
        finally
        {
            chatLog.ReadOnly = wasReadOnly;
        }
    }

    private void CompletePlanWaitLine()
    {
        planWaitStart = -1;
        planWaitLength = 0;
    }

    private void ToggleUi(bool enabled)
    {
        sendButton.Enabled = enabled;
        connectButton.Enabled = enabled;
        decodeImageButton.Enabled = enabled;
        demoButton.Enabled = enabled;
    }

    private void SelectImage()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Title = "Select reference image",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        selectedImagePath = dialog.FileName;
        imagePathLabel.Text = selectedImagePath;

        using var stream = File.OpenRead(selectedImagePath);
        imagePreview.Image?.Dispose( );
        imagePreview.Image = Image.FromStream(stream);
    }

    private void AppendLog(string role, string message)
    {
        var color = role switch
        {
            "you" => Color.LightSkyBlue,
            "assistant" => Color.Gainsboro,
            "tool" => Color.Khaki,
            "plan" => Color.PaleTurquoise,
            "error" => Color.IndianRed,
            _ => Color.MediumSeaGreen,
        };

        chatLog.SelectionColor = color;
        chatLog.AppendText($"[{role}] {message}{Environment.NewLine}{Environment.NewLine}");
        chatLog.SelectionStart = chatLog.TextLength;
        chatLog.ScrollToCaret( );
    }

    private static string FormatToolList(IReadOnlyList<string> toolNames)
    {
        if (toolNames.Count == 0)
        {
            return "No tools returned by MCP.";
        }

        const int maxVisibleTools = 12;
        var builder = new StringBuilder( );
        var count = Math.Min(maxVisibleTools, toolNames.Count);

        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(toolNames[index]);
        }

        if (toolNames.Count > maxVisibleTools)
        {
            builder.Append($", ... (+{toolNames.Count - maxVisibleTools} more)");
        }

        return builder.ToString( );
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        runtime?.Dispose( );
        imagePreview.Image?.Dispose( );
        base.OnFormClosing(e);
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (!IsHandleCreated || chatSplit.IsDisposed)
        {
            return;
        }

        var availableWidth = chatSplit.ClientSize.Width;
        if (availableWidth <= 0)
        {
            return;
        }

        var minPanel1 = chatSplit.Panel1MinSize;
        var minPanel2 = chatSplit.Panel2MinSize;
        var requiredWidth = minPanel1 + minPanel2 + chatSplit.SplitterWidth;

        if (availableWidth <= requiredWidth + 16)
        {
            chatSplit.Panel2Collapsed = true;
            return;
        }

        chatSplit.Panel2Collapsed = false;

        var minDistance = minPanel1;
        var maxDistance = availableWidth - minPanel2 - chatSplit.SplitterWidth;
        if (maxDistance < minDistance)
        {
            chatSplit.Panel2Collapsed = true;
            return;
        }

        var preferredDistance = availableWidth - 340;
        chatSplit.SplitterDistance = Math.Clamp(preferredDistance, minDistance, maxDistance);
    }

    private static string[] SplitCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var args = new List<string>( );
        var current = new StringBuilder( );
        var inQuotes = false;

        foreach (var ch in commandLine)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString( ));
                    current.Clear( );
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            args.Add(current.ToString( ));
        }

        return [.. args];
    }

    private async void btnLaunchSolidoworksAndTestConnection_Click(object sender, EventArgs e)
    {
        try
        {
            await ConnectAsync( ).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("error", ex.Message);
        }
    }
}

