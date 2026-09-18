namespace SolidworksChatClient;

using SolidworksChatClient.Runtime;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

public partial class MainForm : Form
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new( )
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private ChatRuntime? runtime;
    private string? selectedImagePath;
    private bool connecting;
    private bool hasPendingPlanApproval;
    private bool isPlanApproved;
    private CancellationTokenSource? currentOperationCts;
    private int planWaitStart = -1;
    private int planWaitLength;

    public MainForm()
    {
        InitializeComponent( );
        Resize += MainForm_Resize;
        inputBox.KeyDown += InputBox_KeyDown;
        inputBox.PlaceholderText = "Describe the SolidWorks task, then press Enter to send...";

        MainForm_Resize(this, EventArgs.Empty);
        UpdatePlanActionButtons( );
        settingsHint.Text = $"MCP executable path: {GetDefaultMcpCommandPath( )}";
        AppendLog("system", "Open Settings, connect, and the client will validate Ollama + MCP before chat starts.");
        AppendLog("system", "Tip: Enter sends message, Shift+Enter adds a new line. Type /new to start a fresh chat.");
    }


    public void PopulateDemos()
    {

        /*
        Create a Simple Box
1. sw_create_part()
2. sw_create_sketch(plane='Front Plane')
3. sw_sketch_rectangle(x1=0, y1=0, x2=100, y2=60)
4. sw_exit_sketch()
5. sw_extrude(depth=40)
6. sw_save_document(file_path='C:/parts/box.sldprt')
Parametric Cylinder
1. sw_create_part()
2. sw_create_sketch(plane='Front Plane')
3. sw_sketch_circle(cx=0, cy=0, radius=25)
4. sw_exit_sketch()
5. sw_extrude(depth=100)
6. sw_add_equation('"Height" = 100mm')
7. sw_add_equation('"D1@Boss-Extrude1" = "Height"')
Assembly with Mates
1. sw_create_assembly()
2. sw_insert_component(file_path='C:/parts/base.sldprt', fixed=True)
3. sw_insert_component(file_path='C:/parts/shaft.sldprt', z=50)
4. sw_add_mate(mate_type='CONCENTRIC', entity1='Face<1>@shaft-1', entity2='Face<1>@base-1')
5. sw_add_mate(mate_type='COINCIDENT', entity1='Face<2>@shaft-1', entity2='Face<2>@base-1')
Sheet Metal Bracket (with bend)
1. sw_create_part()
2. sw_create_sketch(plane='Top Plane')
3. sw_sketch_rectangle(x1=0, y1=0, x2=200, y2=100)
4. sw_exit_sketch()
5. sw_base_flange(thickness=2.0, bend_radius=1.5)
6. sw_create_sketch(plane='<top face>')
7. sw_sketch_line(...)  # bend line
8. sw_exit_sketch()
9. sw_sketched_bend(angle=90)
10. sw_flatten_sheet_metal()
11. sw_export_flat_pattern('C:/parts/bracket_flat.dxf')
Aluminum Part with Custom Color and Mass Analysis
1. sw_create_part()
2. sw_create_sketch('Front Plane')
3. sw_sketch_circle(0, 0, 25)
4. sw_exit_sketch()
5. sw_extrude(depth=100)
6. sw_set_material('Aluminum 6061-T6')
7. sw_set_appearance_color(r=180, g=180, b=200, transparency=0.0)
8. sw_get_mass_properties()  # returns mass, volume, COG
Drawing with Multiple Views + GD&T
1. sw_create_drawing()
2. sw_add_drawing_view(view_type='front', x=100, y=200, scale=1.0)
3. sw_add_projected_view(parent_view_name='Drawing View1', direction='right')
4. sw_add_detail_view(x=80, y=150, radius=15, scale=2.0, label='A')
5. sw_add_centerline(view_name='Drawing View1')
6. sw_add_geometric_tolerance(gtol_text='Position 0.1 A B C', x=50, y=80)
7. sw_add_surface_finish(symbol_type='machining', roughness='3.2')
8. sw_add_bom_table()
9. sw_export_drawing_pdf('C:/drawings/part.pdf')


        */
        //const string demoPrompt = "Create a complete 4-bar linkage demo using available MCP tools only. Steps: 1) create a new part and sketch linkage plates with holes, 2) extrude features, 3) set key dimensions, 4) rebuild model, 5) create drawing from model, 6) add front and isometric views, 7) summarize generated artifacts and remaining manual CAD steps if any.";
        // https://help.solidworks.com/2026/english/api
        const string demoPrompt = @"
                                    CAD 4-bar linkage parts and align in assemblky using only the available MCP tools.
                                    
                                    Objective:

                                    Build a fully defined parametric 4-bar linkage model, generate the required CAD parts and assembly files and provide a concise summary of the results.
                                    List missing or desired mcp tools that would be needed to fully automate the 4-bar linkage creation process.  
                                    
                                    Workflow:
                                    1. Create ground link, crank, coupler, and rocker CAD parts for 4-bar linkage assembly
                                    2. Sketch each linkage components (ground link, crank, coupler, and rocker), including all required hole locations.
                                    3. Apply geometric constraints and dimensions to fully define each sketch.
                                    4. Extrude the sketches into solid bodies with appropriate feature names.
                                    5. Set and document the critical linkage dimensions (link lengths, hole diameters, plate thicknesses, and center-to-center distances).
                                    6. Rebuild/regenerate the model and verify that all features are successfully created without errors.
                                    7. Save all solidworks parts with relevant names.
                                    
                                    8. Create a drawing based on the completed model.
                                    9. Insert at minimum:
                                    - One front view
                                    - One isometric view
                                    10. Ensure drawing views are properly scaled and updated.
                                    
                                    Final Output:
                                    - List every generated artifact (part files, drawings, etc.).
                                    - Report the final dimensions used in the model.
                                    - Confirm whether the model and drawing were created successfully.
                                    - Identify any steps that could not be completed through MCP tools alone.
                                    - Provide any remaining manual CAD actions required to achieve a production-ready model.
                                    
                                    ";

        //Load up Combo comboBox1 with demo

        // opon click of DemoButton_Click

      //  await SendPromptAsync(demoPrompt).ConfigureAwait(true);

    }
    private async void ConnectButton_Click(object? sender, EventArgs e) => await ConnectAsync( ).ConfigureAwait(true);

    private async void SendButton_Click(object? sender, EventArgs e) => await SendCurrentInputAsync( ).ConfigureAwait(true);

    private async void DecodeImageButton_Click(object? sender, EventArgs e) => await SendImageDecodePromptAsync( ).ConfigureAwait(true);

  
    private void StopButton_Click(object? sender, EventArgs e)
    {
        var operation = currentOperationCts;
        if (operation is not null)
        {

            operation.Cancel( );
            AppendLog("system", "Stop requested. Cancelling current activity...");
            statusLabel.Text = "Status: stopping...";
        }

        StartNewChat( );
    }

    private void ApprovePlanButton_Click(object? sender, EventArgs e)
    {
        if (!hasPendingPlanApproval || isPlanApproved)
        {
            return;
        }

        isPlanApproved = true;
        AppendLog("system", "Plan approved. Click Execute to run MCP actions.");
        UpdatePlanActionButtons( );
    }

    private void RejectPlanButton_Click(object? sender, EventArgs e)
    {
        RejectPendingPlan("Plan rejected. Send an updated request when ready.");
    }

    private async void ExecutePlanButton_Click(object? sender, EventArgs e)
    {
        if (!hasPendingPlanApproval)
        {
            return;
        }

        try
        {
            await ExecuteApprovedPlanAsync( ).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("error", ex.Message);
        }
    }

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
        var operation = BeginOperation( );
        ToggleUi(false);
        statusLabel.Text = "Status: connecting...";

        try
        {
            var cancellationToken = operation.Token;
            var configuredModel = modelBox.Text.Trim( );
            var ollamaStatus = await OllamaClient.TestConnectionAsync(ollamaUrlBox.Text.Trim( ), configuredModel, cancellationToken).ConfigureAwait(true);
            AppendLog("system", $"Ollama reachable. Discovered {ollamaStatus.AvailableModelCount} model(s).");

            if (!ollamaStatus.IsModelAvailable)
            {
                AppendLog("error", $"Configured model '{configuredModel}' is not in Ollama tags. Continue only after pull/update.");
            }

            var mcpCommand = GetDefaultMcpCommandPath( );
            if (!File.Exists(mcpCommand))
            {
                throw new FileNotFoundException($"MCP executable was not found: {mcpCommand}");
            }

#if DEBUG
            if (Debugger.IsAttached && runtime is not null)
            {
                var reusedMcpStatus = await runtime.TestMcpConnectionAsync(cancellationToken).ConfigureAwait(true);
                AppendLog("system", $"Reusing existing MCP runtime. {reusedMcpStatus.ToolCount} tool(s) available.");
                AppendLog("plan", $"Tool catalog: {FormatToolList(reusedMcpStatus.ToolNames)}");

                var reusedSolidWorksStatus = await runtime.WarmupSolidWorksAsync(cancellationToken).ConfigureAwait(true);
                AppendLog(reusedSolidWorksStatus.Succeeded ? "system" : "error", reusedSolidWorksStatus.Message);

                statusLabel.Text = reusedSolidWorksStatus.Succeeded
                    ? "Status: connected + SolidWorks ready"
                    : "Status: connected (SolidWorks warm-up skipped/failed)";

                return;
            }
#endif

            runtime?.Dispose( );
            runtime = new ChatRuntime(new ChatSettings(
                ollamaUrlBox.Text.Trim( ),
                configuredModel,
                mcpCommand,
                SplitCommandLine(mcpArgsBox.Text.Trim( ))));

            await runtime.InitializeAsync(cancellationToken).ConfigureAwait(true);

            var mcpStatus = await runtime.TestMcpConnectionAsync(cancellationToken).ConfigureAwait(true);
            AppendLog("system", $"MCP connected. {mcpStatus.ToolCount} tool(s) available.");
            AppendLog("plan", $"Tool catalog: {FormatToolList(mcpStatus.ToolNames)}");

            var solidWorksStatus = await runtime.WarmupSolidWorksAsync(cancellationToken).ConfigureAwait(true);
            AppendLog(solidWorksStatus.Succeeded ? "system" : "error", solidWorksStatus.Message);

            statusLabel.Text = solidWorksStatus.Succeeded
                ? "Status: connected + SolidWorks ready"
                : "Status: connected (SolidWorks warm-up skipped/failed)";
        }
        catch (OperationCanceledException)
        {
            AppendLog("system", "Connection activity cancelled.");
            statusLabel.Text = "Status: cancelled";
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
            EndOperation(operation);
        }
    }

    private async Task SendCurrentInputAsync()
    {
        var text = inputBox.Text.Trim( );
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (text.Equals("/new", StringComparison.OrdinalIgnoreCase))
        {
            inputBox.Text = string.Empty;
            StartNewChat( );
            return;
        }

        if (hasPendingPlanApproval)
        {
            if (await TryHandlePendingPlanCommandAsync(text).ConfigureAwait(true))
            {
                inputBox.Text = string.Empty;
                return;
            }

            RejectPendingPlan("Previous plan discarded. Drafting a new plan from your updated request...");
        }

        await SendPromptAsync(text).ConfigureAwait(true);
    }

    private void StartNewChat()
    {
        if (runtime is null)
        {
            AppendLog("system", "Not connected. Connect first, then use /new to reset chat context.");
            return;
        }

        runtime.ResetConversation( );
        hasPendingPlanApproval = false;
        isPlanApproved = false;
        UpdatePlanActionButtons( );
        AppendLog("system", "Started a new chat. Previous plan and conversation context were cleared.");
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

   

    private async Task SendPromptAsync(string prompt)
    {
        if (runtime is null)
        {
            AppendLog("system", "Not connected. Open Settings tab and click Connect first.");
            return;
        }

        var operation = BeginOperation( );
        ToggleUi(false);
        inputBox.Text = string.Empty;
        AppendLog("you", prompt);
        BeginPlanWaitLine( );

        using var waitCounterCancellation = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
        var waitCounterTask = RunPlanWaitCounterAsync(waitCounterCancellation.Token);

        try
        {
            var imageBase64 = attachImageCheckBox.Checked && !string.IsNullOrWhiteSpace(selectedImagePath) && File.Exists(selectedImagePath)
                ? Convert.ToBase64String(await File.ReadAllBytesAsync(selectedImagePath, operation.Token).ConfigureAwait(true))
                : null;

            var planProposal = await runtime.ProposePlanAsync(prompt, imageBase64, operation.Token).ConfigureAwait(true);

            if (!planProposal.RequiresApproval)
            {
                waitCounterCancellation.Cancel( );
                await waitCounterTask.ConfigureAwait(true);
                CompletePlanWaitLine( );
                AppendLog("plan", "Assistant completed planning and returned a final response.");
                AppendLog("assistant", planProposal.PlanSummary);
                AppendPlanDiagnostics(planProposal);
                AppendLog("system", "No executable tool calls were detected. Ask for 'MCP tool calls only' or refine your request.");
                return;
            }

            waitCounterCancellation.Cancel( );
            await waitCounterTask.ConfigureAwait(true);
            CompletePlanWaitLine( );

            AppendLog("plan", $"Proposed plan: {planProposal.PlanSummary}");
            AppendLog("plan", $"Plan diagnostics: source={planProposal.ParseSource}; toolCount={planProposal.PlannedToolCalls.Count}");
            AppendPlanDiagnostics(planProposal);
            foreach (var plannedCall in planProposal.PlannedToolCalls)
            {
                AppendLog("plan", $"proposed> {plannedCall.Name} [source={plannedCall.Source}]{Environment.NewLine}{FormatHumanReadableJson(plannedCall.Arguments)}");
            }

            hasPendingPlanApproval = true;
            isPlanApproved = false;
            UpdatePlanActionButtons( );
            AppendLog("system", "Plan ready. Use Approve/Reject/Execute buttons or type /approve, /reject, /execute, /new.");
        }
        catch (OperationCanceledException)
        {
            waitCounterCancellation.Cancel( );
            await waitCounterTask.ConfigureAwait(true);
            CompletePlanWaitLine( );
            RejectPendingPlan("Request cancelled.");
        }
        catch (Exception ex)
        {
            waitCounterCancellation.Cancel( );
            await waitCounterTask.ConfigureAwait(true);
            CompletePlanWaitLine( );
            RejectPendingPlan(ex.Message, "error");
        }
        finally
        {
            ToggleUi(true);
            EndOperation(operation);
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

    private async Task<bool> TryHandlePendingPlanCommandAsync(string commandText)
    {
        if (runtime is null)
        {
            hasPendingPlanApproval = false;
            isPlanApproved = false;
            UpdatePlanActionButtons( );
            return false;
        }

        if (commandText.Equals("/reject", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog("you", commandText);
            RejectPendingPlan("Plan rejected. Send an updated request when ready.");
            return true;
        }

        if (commandText.Equals("/approve", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog("you", commandText);
            if (!hasPendingPlanApproval)
            {
                AppendLog("system", "No pending plan to approve.");
                return true;
            }

            if (isPlanApproved)
            {
                AppendLog("system", "Plan is already approved. Use /execute to run.");
                return true;
            }

            isPlanApproved = true;
            AppendLog("system", "Plan approved. Use /execute to run it.");
            UpdatePlanActionButtons( );
            return true;
        }

        if (!commandText.Equals("/execute", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        AppendLog("you", commandText);
        await ExecuteApprovedPlanAsync( ).ConfigureAwait(true);
        return true;
    }

    private async Task ExecuteApprovedPlanAsync()
    {
        if (runtime is null)
        {
            return;
        }

        if (!hasPendingPlanApproval)
        {
            AppendLog("system", "No pending plan to execute.");
            return;
        }

        if (!isPlanApproved)
        {
            AppendLog("system", "Approve the plan first, then execute.");
            return;
        }

        var operation = BeginOperation( );
        ToggleUi(false);

        try
        {
            AppendLog("system", "Executing approved MCP plan...");
            var result = await runtime.ExecuteApprovedPlanAsync(operation.Token).ConfigureAwait(true);
            hasPendingPlanApproval = false;
            isPlanApproved = false;
            UpdatePlanActionButtons( );

            foreach (var progressUpdate in result.ProgressUpdates)
            {
                AppendLog("plan", progressUpdate);
            }

            AppendLog("assistant", result.FinalResponse);

            if (!string.IsNullOrWhiteSpace(result.FeedbackImagePath) && File.Exists(result.FeedbackImagePath))
            {
                LoadPreviewImage(result.FeedbackImagePath, "Generated feedback image ready for the next prompt.");
                attachImageCheckBox.Checked = true;
                AppendLog("system", $"Visual feedback captured: {result.FeedbackImagePath}");
            }

            if (result.FailureDiagnostic is not null)
            {
                AppendLog("error", $"Execution failures: {result.FailureDiagnostic.FailedToolCount}");
                foreach (var summary in result.FailureDiagnostic.FailureSummaries)
                {
                    AppendLog("error", summary);
                }
            }

            foreach (var toolCall in result.ToolCalls)
            {
                var stepPrefix = $"[{toolCall.StepNumber}/{toolCall.TotalSteps}] ";
                AppendLog("tool", $"> {stepPrefix}{toolCall.Name}{Environment.NewLine}{FormatHumanReadableJson(toolCall.Arguments)}");
                AppendLog(toolCall.Succeeded ? "tool" : "error", $"< {stepPrefix}{FormatHumanReadableJson(toolCall.Result)}");

                if (!string.IsNullOrWhiteSpace(toolCall.DiagnosticSummary))
                {
                    AppendLog("plan", $"diag {stepPrefix}{toolCall.DiagnosticSummary}");
                }

                if (!string.IsNullOrWhiteSpace(toolCall.RawResponse) && !string.Equals(toolCall.RawResponse, toolCall.Result, StringComparison.Ordinal))
                {
                    AppendLog("plan", $"raw {stepPrefix}{FormatHumanReadableJson(toolCall.RawResponse)}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            RejectPendingPlan("Execution cancelled.");
        }
        catch (Exception ex)
        {
            RejectPendingPlan($"Execution failed: {ex.Message}", "error");
        }
        finally
        {
            ToggleUi(true);
            EndOperation(operation);
            inputBox.Focus( );
        }
    }

    private void RejectPendingPlan(string message, string role = "system")
    {
        runtime?.RejectPendingPlan( );
        hasPendingPlanApproval = false;
        isPlanApproved = false;
        UpdatePlanActionButtons( );
        AppendLog(role, message);
    }

    private void UpdatePlanActionButtons()
    {
        var canInteract = currentOperationCts is null;
        approvePlanButton.Enabled = canInteract && hasPendingPlanApproval && !isPlanApproved;
        rejectPlanButton.Enabled = canInteract && hasPendingPlanApproval;
        executePlanButton.Enabled = canInteract && hasPendingPlanApproval && isPlanApproved;
    }

    private CancellationTokenSource BeginOperation()
    {
        currentOperationCts?.Dispose( );
        currentOperationCts = new CancellationTokenSource( );
        stopButton.Enabled = true;
        UpdatePlanActionButtons( );
        return currentOperationCts;
    }

    private void EndOperation(CancellationTokenSource operation)
    {
        if (ReferenceEquals(currentOperationCts, operation))
        {
            currentOperationCts = null;
            stopButton.Enabled = false;
            UpdatePlanActionButtons( );
        }

        operation.Dispose( );
    }

    private void ToggleUi(bool enabled)
    {
        sendButton.Enabled = enabled;
        connectButton.Enabled = enabled;
        decodeImageButton.Enabled = enabled;
        demoButton.Enabled = enabled;

        if (!enabled)
        {
            stopButton.Enabled = true;
        }
        else if (currentOperationCts is null)
        {
            stopButton.Enabled = false;
        }

        UpdatePlanActionButtons( );
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

        LoadPreviewImage(dialog.FileName);
    }

    private void LoadPreviewImage(string imagePath, string? statusMessage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        selectedImagePath = imagePath;
        imagePathLabel.Text = imagePath;

        using var stream = File.OpenRead(imagePath);
        imagePreview.Image?.Dispose( );
        imagePreview.Image = Image.FromStream(stream);

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            statusLabel.Text = $"Status: {statusMessage}";
        }
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

        var formattedMessage = FormatHumanReadableJson(message);
        var normalized = formattedMessage.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');

        chatLog.SelectionColor = color;

        if (lines.Length == 0)
        {
            chatLog.AppendText($"[{role}] {Environment.NewLine}{Environment.NewLine}");
        }
        else if (lines.Length == 1)
        {
            chatLog.AppendText($"[{role}] {lines[0]}{Environment.NewLine}{Environment.NewLine}");
        }
        else
        {
            chatLog.AppendText($"[{role}] {lines[0]}{Environment.NewLine}");

            for (var index = 1; index < lines.Length; index++)
            {
                chatLog.AppendText($"       {lines[index]}{Environment.NewLine}");
            }

            chatLog.AppendText(Environment.NewLine);
        }

        chatLog.SelectionStart = chatLog.TextLength;
        chatLog.ScrollToCaret( );
    }

    private void AppendPlanDiagnostics(ChatPlanProposal planProposal)
    {
        if (planProposal.Diagnostics is null || planProposal.Diagnostics.Count == 0)
        {
            return;
        }

        foreach (var diagnostic in planProposal.Diagnostics)
        {
            AppendLog("plan", $"diag {diagnostic}");
        }
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
        currentOperationCts?.Cancel( );
        currentOperationCts?.Dispose( );
        currentOperationCts = null;

        runtime?.Dispose( );
        imagePreview.Image?.Dispose( );
        base.OnFormClosing(e);
    }

    private static string GetDefaultMcpCommandPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("SOLIDWORKS_MCP_COMMAND");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var appFolderPath = Path.Combine(AppContext.BaseDirectory, "SolidworksMCP.exe");

#if DEBUG
        if (Debugger.IsAttached)
        {
            var solutionBuildPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "SolidworksMCP",
                "bin",
                "Debug",
                "net9.0",
                "SolidworksMCP.exe"));

            if (File.Exists(solutionBuildPath))
            {
                return solutionBuildPath;
            }
        }
#endif

        return appFolderPath;
    }

    private static string FormatHumanReadableJson(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        var trimmed = message.Trim( );

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed[7..^3].Trim( );
        }

        if (!((trimmed.StartsWith('{') && trimmed.EndsWith('}')) || (trimmed.StartsWith('[') && trimmed.EndsWith(']'))))
        {
            return message;
        }

        try
        {
            using var json = JsonDocument.Parse(trimmed);
            return JsonSerializer.Serialize(json.RootElement, PrettyJsonOptions);
        }
        catch (JsonException)
        {
            return message;
        }
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
        ConnecttoSolidWorks( );
    }

    private async void ConnecttoSolidWorks()
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

    private void MainForm_Load(object sender, EventArgs e)
    {
        // ConnectAsync to Solidworks
        ConnecttoSolidWorks( );
    }
}

