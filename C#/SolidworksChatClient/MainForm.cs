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

    private const string DemoWorkingRoot = @"C:\SwAutomation";

    private static readonly (string Name, string Prompt)[] DemoSamples =
    [
    ("4-Bar Linkage", """
        Create a complete parametric 4-bar linkage mechanism using available MCP tools only.
        Objective:
        Build a fully defined 4-bar linkage consisting of:
        - Ground Link (fixed base)
        - Input Crank
        - Coupler Link
        - Output Rocker

        Component Definitions:

        1. Ground Link
            - Create a new part named Ground_Link.
            - Sketch a rectangular plate.
            - Add two pivot holes.
            - Hole diameter: 10 mm.
            - Center-to-center distance: 120 mm.
            - Plate thickness: 8 mm.
            - Fully define all sketch entities.
            - Extrude the plate.

        2. Input Crank
            - Create a new part named Input_Crank.
            - Length: 40 mm pivot-to-pivot.
            - Width: 15 mm.
            - Hole diameter: 10 mm at both ends.
            - Thickness: 8 mm.
            - Fully define sketch dimensions.
            - Extrude the part.

        3. Coupler Link
            - Create a new part named Coupler_Link.
            - Length: 100 mm pivot-to-pivot.
            - Width: 15 mm.
            - Hole diameter: 10 mm at both ends.
            - Thickness: 8 mm.
            - Fully define sketch dimensions.
            - Extrude the part.

        4. Output Rocker
            - Create a new part named Output_Rocker.
            - Length: 80 mm pivot-to-pivot.
            - Width: 15 mm.
            - Hole diameter: 10 mm at both ends.
            - Thickness: 8 mm.
            - Fully define sketch dimensions.
            - Extrude the part.

        Model Requirements:
        - Rebuild each part after feature creation.
        - Verify all sketches are fully defined.
        - Save all generated parts.

        Assembly Creation:

        5. Create a new assembly named Four_Bar_Linkage.

        6. Insert components:
            - Ground_Link
            - Input_Crank
            - Coupler_Link
            - Output_Rocker

        7. Apply mates:
            - Fix Ground_Link.
            - Concentric mate between crank and left ground pivot.
            - Concentric mate between crank and coupler.
            - Concentric mate between coupler and rocker.
            - Concentric mate between rocker and right ground pivot.
            - Add coincident face mates as required to remove unwanted translation.
            - Ensure only rotational motion remains at pivots.

        Motion Verification:

        8. Rotate the input crank through a valid angle.
            - Rebuild assembly.
            - Confirm linkage motion is mechanically valid.
            - Report any over-defined or under-defined mate conditions.

        Drawing Creation:

        9. Create a drawing named Four_Bar_Linkage_Drawing.

        10. Add views:
            - Front View
            - Top View
            - Right View
            - Isometric View

        11. Apply annotations:
            - Overall linkage dimensions.
            - Link lengths.
            - Hole diameters.
            - Hole spacing.
            - Component names.

        Output Summary:

        12. Provide a final report containing:
            - Parts created.
            - Assembly created.
            - Drawings created.
            - Dimensions used.
            - Mates applied.
            - Rebuild status.
            - Any missing MCP capabilities.
            - Any manual SOLIDWORKS steps still required.

        Constraints:
        - Use MCP tools only.
        - Do not assume unsupported CAD operations exist.
        - Validate success after each step.
    """),
    ("Simple Box", """
        Create a simple box part using available MCP tools only.

        Steps:
        1. Create a new part.
        2. Sketch a centered rectangle on the Front Plane.
        3. Extrude the sketch.
        4. Save the part as box.sldprt.
        """),
                ("Parametric Cylinder", """
        Create a parametric cylinder using available MCP tools only.

        Steps:
        1. Create a new part.
        2. Sketch a circle on the Front Plane.
        3. Extrude the sketch.
        4. Add a height equation.
        5. Link the extrusion dimension to the equation.
     """),
    ("Assembly with Mates", """
        Create a small assembly with mates using available MCP tools only.

        Steps:
        1. Create a new assembly.
        2. Insert a base component and fix it.
        3. Insert a shaft component.
        4. Add concentric and coincident mates between the parts.
    """),
    ("Sheet Metal Bracket", """
        Create a sheet metal bracket using available MCP tools only.

        Steps:
        1. Create a new part.
        2. Sketch a rectangle on the Top Plane.
        3. Apply a base flange.
        4. Sketch a bend line on the top face.
        5. Add a sketched bend.
        6. Flatten the sheet metal and export the flat pattern.
    """),
    ("Aluminum Part", """
        Create an aluminum part with analysis using available MCP tools only.

        Steps:
        1. Create a new part.
        2. Sketch a circle on the Front Plane.
        3. Extrude the sketch.
        4. Apply Aluminum 6061-T6.
        5. Set the appearance color.
        6. Report mass properties.
    """),
    ("Drawing Views + GD&T", """
        Create a drawing with multiple views and GD&T using available MCP tools only.

        Steps:
        1. Create a new drawing.
        2. Add a front view.
        3. Add a projected right view or isometric view.
        4. Add a detail view.
        5. Add centerlines and geometric tolerance.
        6. Add a BOM table and export the drawing.
    """),
    ];

    private ChatRuntime? runtime;
    private string? selectedImagePath;
    private bool connecting;
    private bool hasPendingPlanApproval;
    private bool isPlanApproved;
    private bool awaitingStepApproval;
    private string? pendingStepApprovalPrompt;
    private CancellationTokenSource? currentOperationCts;
    private int planWaitStart = -1;
    private int planWaitLength;

    public MainForm()
    {
        InitializeComponent( );
        Resize += MainForm_Resize;
        inputBox.KeyDown += InputBox_KeyDown;
        inputBox.PlaceholderText = "Describe the SolidWorks task, then press Enter to send...";
        comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox1.SelectedIndexChanged += DemoComboBox_SelectedIndexChanged;
        demoButton.Click += DemoButton_Click;
        humanInLoopCheckBox.Checked = false;

        PopulateDemos( );
        MainForm_Resize(this, EventArgs.Empty);
        UpdatePlanActionButtons( );
        settingsHint.Text = $"MCP executable path: {GetDefaultMcpCommandPath( )}";
        AppendLog("system", "Open Settings, connect, and the client will validate Ai Box + MCP before chat starts.");
        AppendLog("system", "Tip: Enter sends message, Shift+Enter adds a new line. Type /new to start a fresh chat.");
    }


    public void PopulateDemos()
    {
        comboBox1.BeginUpdate( );

        try
        {
            comboBox1.Items.Clear( );

            foreach (var sample in DemoSamples)
            {
                comboBox1.Items.Add(sample.Name);
            }

            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }
        }
        finally
        {
            comboBox1.EndUpdate( );
        }

        UpdateDemoSelectionUi( );
    }

    private void DemoComboBox_SelectedIndexChanged(object? sender, EventArgs e) => UpdateDemoSelectionUi( );

    private async void DemoButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var demoPrompt = GetSelectedDemoPrompt( );
            if (demoPrompt is null)
            {
                AppendLog("system", "Select a demo sample first.");
                return;
            }

            await SendPromptAsync(ApplyDemoWorkingRoot(demoPrompt)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("error", ex.Message);
        }
    }

    private string? GetSelectedDemoPrompt( )
    {
        if (comboBox1.SelectedIndex < 0 || comboBox1.SelectedIndex >= DemoSamples.Length)
        {
            return null;
        }

        return DemoSamples[comboBox1.SelectedIndex].Prompt;
    }

    private static string ApplyDemoWorkingRoot(string prompt)
    {
        Directory.CreateDirectory(DemoWorkingRoot);

        return $"{prompt.Trim()}{Environment.NewLine}{Environment.NewLine}Working/save root location for this demo: {DemoWorkingRoot}{Environment.NewLine}- Use this root for all generated parts, assemblies, drawings, exports, and saved artifacts.{Environment.NewLine}- Create and reuse demo-specific subfolders under this root as needed.";
    }

    private void UpdateDemoSelectionUi( )
    {
        demoButton.Text = comboBox1.SelectedItem is string demoName && !string.IsNullOrWhiteSpace(demoName)
            ? $"Demo: {demoName}"
            : "Demo:";
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
            AppendLog("system", $"Ai Box reachable. Discovered {ollamaStatus.AvailableModelCount} model(s).");

            if (!ollamaStatus.IsModelAvailable)
            {
                AppendLog("error", $"Configured model '{configuredModel}' is not available on Ai Box. Continue only after pull/update.");
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

        if (awaitingStepApproval)
        {
            if (await TryHandlePendingStepApprovalAsync(text).ConfigureAwait(true))
            {
                inputBox.Text = string.Empty;
                return;
            }
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
        awaitingStepApproval = false;
        pendingStepApprovalPrompt = null;
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

        const string decodePrompt = "Analyze this screenshot or image for SolidWorks/CAD intent, identify visible failures or missing features, propose exact recovery steps, and suggest any scope changes, dimensions, constraints, BOM candidates, and drawing views required. After analysis, use tools where possible to continue model creation.";
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
            AppendLog("system", "Plan ready. Use Approve/Reject/Execute buttons or type /approve, /reject, /execute, /retry, /new.");
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

        if (commandText.Equals("/retry", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog("you", commandText);
            if (!hasPendingPlanApproval)
            {
                AppendLog("system", "No failed plan is available to retry.");
                return true;
            }

            if (!isPlanApproved)
            {
                AppendLog("system", "Approve the plan first, then retry.");
                return true;
            }

            await ExecuteApprovedPlanAsync( ).ConfigureAwait(true);
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

    private async Task<bool> TryHandlePendingStepApprovalAsync(string commandText)
    {
        if (!awaitingStepApproval)
        {
            return false;
        }

        if (IsAffirmativeStepApprovalResponse(commandText))
        {
            AppendLog("you", commandText);
            awaitingStepApproval = false;
            pendingStepApprovalPrompt = null;
            UpdatePlanActionButtons( );
            await ExecuteApprovedPlanAsync( ).ConfigureAwait(true);
            return true;
        }

        if (commandText.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        AppendLog("you", commandText);

        if (commandText.Length < 8)
        {
            AppendLog("system", "Reply yes to continue, or describe the revision needed for the paused step.");
            return true;
        }

        RejectPendingPlan("Revision requested for the paused step. Drafting a revised prompt from your update...");
        await SendPromptAsync(commandText).ConfigureAwait(true);
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
            var result = await runtime.ExecuteApprovedPlanAsync(humanInLoopCheckBox.Checked, operation.Token).ConfigureAwait(true);

            foreach (var progressUpdate in result.ProgressUpdates)
            {
                AppendLog("plan", progressUpdate);
            }

            if (result.AwaitingHumanApproval)
            {
                awaitingStepApproval = true;
                pendingStepApprovalPrompt = result.HumanApprovalPrompt;
                AppendLog("system", result.HumanApprovalPrompt ?? result.FinalResponse);
            }
            else
            {
                awaitingStepApproval = false;
                pendingStepApprovalPrompt = null;
                AppendLog("assistant", result.FinalResponse);
            }

            if (!string.IsNullOrWhiteSpace(result.FeedbackImagePath) && File.Exists(result.FeedbackImagePath))
            {
                LoadPreviewImage(result.FeedbackImagePath, "Captured screenshot or feedback image is ready for analysis.");
                attachImageCheckBox.Checked = true;
                AppendLog("system", $"Visual feedback captured: {result.FeedbackImagePath}");
                AppendLog("system", "Use Decode + Plan CAD Steps to inspect the screenshot before retrying or redefining the scope.");
            }

            if (result.StoppedOnFirstFailure)
            {
                hasPendingPlanApproval = true;
                isPlanApproved = true;
                awaitingStepApproval = result.AwaitingHumanApproval;
                pendingStepApprovalPrompt = result.HumanApprovalPrompt;
                UpdatePlanActionButtons( );

                var failureDiagnostic = result.FailureDiagnostic;
                AppendLog("error", $"Execution stopped on the first failure. Failed tool count: {failureDiagnostic?.FailedToolCount ?? 0}");
                if (failureDiagnostic is not null)
                {
                    foreach (var summary in failureDiagnostic.FailureSummaries)
                    {
                        AppendLog("error", summary);
                    }
                }

                if (result.AwaitingHumanApproval && !string.IsNullOrWhiteSpace(result.HumanApprovalPrompt))
                {
                    AppendLog("system", result.HumanApprovalPrompt);
                }
                else if (!string.IsNullOrWhiteSpace(result.FeedbackImagePath))
                {
                    AppendLog("system", "A screenshot or feedback image is attached for analysis. Retry the same plan or redefine the scope with a revised prompt.");
                }
                else
                {
                    AppendLog("system", "Retry the same plan or redefine the scope with a revised prompt. Attach a screenshot if the failure is visual.");
                }
            }
            else if (!result.AwaitingHumanApproval)
            {
                hasPendingPlanApproval = false;
                isPlanApproved = false;
                awaitingStepApproval = false;
                pendingStepApprovalPrompt = null;
                UpdatePlanActionButtons( );
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
        awaitingStepApproval = false;
        pendingStepApprovalPrompt = null;
        UpdatePlanActionButtons( );
        AppendLog(role, message);
    }

    private static bool IsAffirmativeStepApprovalResponse(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        return normalized is "y" or "yes" or "ok" or "okay" or "continue" or "go" or "approve" or "approved" or "resume" or "proceed" || normalized.StartsWith("yes ", StringComparison.Ordinal) || normalized.StartsWith("continue ", StringComparison.Ordinal);
    }

    private void UpdatePlanActionButtons()
    {
        var canInteract = currentOperationCts is null;
        approvePlanButton.Enabled = canInteract && hasPendingPlanApproval && !isPlanApproved && !awaitingStepApproval;
        rejectPlanButton.Enabled = canInteract && hasPendingPlanApproval;
        executePlanButton.Enabled = canInteract && hasPendingPlanApproval && (isPlanApproved || awaitingStepApproval);
        executePlanButton.Text = awaitingStepApproval ? "Continue" : "Execute";
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
        humanInLoopCheckBox.Enabled = enabled;

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

