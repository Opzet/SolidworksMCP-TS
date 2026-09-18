namespace SolidworksChatClient.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class ChatRuntime : IDisposable
{
    private const int MaxRounds = 8;
    private const string SystemPrompt = "You are a SolidWorks CAD assistant. Use MCP tools whenever CAD actions are required. Be deterministic. For part creation workflows, first enumerate templates with list_part_templates (if available), then select/create the part using create_part, then continue with create_sketch, add_line/add_circle/add_rectangle, exit_sketch, create_extrusion, set_dimension, rebuild_model, create_drawing_from_model, and add_drawing_view. If a required capability is unavailable, clearly state the limitation and continue with available tools.";

    private readonly ChatSettings settings;
    private readonly McpClient mcp;
    private readonly List<OllamaMessage> conversation =
    [
        new("system", SystemPrompt)
    ];

    private List<OllamaToolCall>? pendingToolCalls;

    public ChatRuntime(ChatSettings settings)
    {
        this.settings = settings;
        mcp = new McpClient(settings.McpCommand, settings.McpArgs);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await mcp.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<McpConnectionStatus> TestMcpConnectionAsync(CancellationToken cancellationToken = default)
    {
        var toolNames = await mcp.ListToolNamesAsync(cancellationToken).ConfigureAwait(false);
        return new McpConnectionStatus(toolNames.Count, toolNames);
    }

    public async Task<SolidWorksWarmupStatus> WarmupSolidWorksAsync(CancellationToken cancellationToken = default)
    {
        var toolNames = await mcp.ListToolNamesAsync(cancellationToken).ConfigureAwait(false);

        if (ContainsTool(toolNames, "launch_solidworks_and_check_connection"))
        {
            var launchResult = await mcp.CallToolAsync("launch_solidworks_and_check_connection", new JsonObject(), cancellationToken).ConfigureAwait(false);
            return new SolidWorksWarmupStatus(true, true, launchResult.DisplayText);
        }

        if (!ContainsTool(toolNames, "create_part"))
        {
            return new SolidWorksWarmupStatus(false, false, "MCP does not expose launch/create warm-up tools, so SolidWorks warm-up was skipped.");
        }

        var createResult = await mcp.CallToolAsync("create_part", new JsonObject(), cancellationToken).ConfigureAwait(false);
        var closeNote = string.Empty;
        if (ContainsTool(toolNames, "close_model"))
        {
            var closeResult = await mcp.CallToolAsync("close_model", new JsonObject { ["save"] = false }, cancellationToken).ConfigureAwait(false);
            closeNote = $" Temporary model cleanup: {closeResult.DisplayText}";
        }

        return new SolidWorksWarmupStatus(true, true, $"SolidWorks warm-up via create_part completed. {createResult.DisplayText}{closeNote}");
    }

    public async Task<ChatPlanProposal> ProposePlanAsync(string userPrompt, string? imageBase64, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(userPrompt));
        }

        var userMessage = string.IsNullOrWhiteSpace(imageBase64)
            ? new OllamaMessage("user", userPrompt)
            : new OllamaMessage("user", userPrompt, Images: [imageBase64]);

        conversation.Add(userMessage);

        var tools = await mcp.ListToolsAsOllamaToolsAsync(cancellationToken).ConfigureAwait(false);
        var response = await OllamaClient.ChatAsync(settings.OllamaBaseUrl, settings.Model, conversation, tools, cancellationToken).ConfigureAwait(false);
        conversation.Add(response.Message);

        var parsedFromContent = false;
        var proposedCalls = response.Message.ToolCalls;
        if (proposedCalls is null || proposedCalls.Count == 0)
        {
            proposedCalls = ParseToolCallsFromContent(response.Message.Content);
            parsedFromContent = proposedCalls.Count > 0;
        }

        if (proposedCalls is null || proposedCalls.Count == 0)
        {
            pendingToolCalls = null;
            return new ChatPlanProposal(
                response.Message.Content ?? string.Empty,
                [],
                false,
                parsedFromContent ? "content-json" : "tool-call",
                BuildPlanDiagnostics(response.Message.Content, parsedFromContent, proposedCalls));
        }

        proposedCalls = EnsureTemplateEnumerationStep(proposedCalls, tools);
        pendingToolCalls = [.. proposedCalls];
        var plannedCalls = new List<PlannedToolCall>(pendingToolCalls.Count);
        foreach (var call in pendingToolCalls)
        {
            plannedCalls.Add(new PlannedToolCall(
                call.Function.Name,
                ToJsonObject(call.Function.Arguments).ToJsonString(),
                parsedFromContent ? "content-json" : "tool-call"));
        }

        var summary = parsedFromContent
            ? $"Assistant returned tool steps as text. Parsed {plannedCalls.Count} executable step(s). Review and approve to run."
            : string.IsNullOrWhiteSpace(response.Message.Content)
                ? "Assistant prepared an execution plan with MCP tools."
                : response.Message.Content;

        return new ChatPlanProposal(
            summary,
            plannedCalls,
            true,
            parsedFromContent ? "content-json" : "tool-call",
            BuildPlanDiagnostics(response.Message.Content, parsedFromContent, plannedCalls));
    }

    public void RejectPendingPlan() => pendingToolCalls = null;

    /// <summary>
    /// Clears pending plan state and resets the chat conversation to the initial system prompt.
    /// </summary>
    public void ResetConversation()
    {
        pendingToolCalls = null;
        conversation.Clear();
        conversation.Add(new OllamaMessage("system", SystemPrompt));
    }

    public async Task<ChatTurnResult> ExecuteApprovedPlanAsync(CancellationToken cancellationToken = default)
    {
        if (pendingToolCalls is null || pendingToolCalls.Count == 0)
        {
            throw new InvalidOperationException("No pending plan to execute.");
        }

        var currentCalls = pendingToolCalls;
        pendingToolCalls = null;

        var toolCalls = new List<ToolExecution>();
        var progressUpdates = new List<string>
        {
            "Approval received. Executing MCP plan.",
        };
        var totalSteps = currentCalls.Count;
        var completedSteps = 0;

        for (var round = 0; round < MaxRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var call in currentCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stepNumber = completedSteps + 1;
                progressUpdates.Add($"Executing tool {stepNumber}/{totalSteps}: {call.Function.Name}");

                var argsObject = ToJsonObject(call.Function.Arguments);
                var result = await mcp.CallToolAsync(call.Function.Name, argsObject, cancellationToken).ConfigureAwait(false);
                var succeeded = IsSuccessfulToolResult(result.DisplayText);
                var failureSummary = succeeded ? null : BuildFailureSummary(call.Function.Name, result.DisplayText);
                var diagnosticSummary = BuildToolDiagnosticSummary(call.Function.Name, argsObject, result, succeeded);

                toolCalls.Add(new ToolExecution(
                    stepNumber,
                    totalSteps,
                    call.Function.Name,
                    argsObject.ToJsonString(),
                    result.DisplayText,
                    succeeded,
                    failureSummary,
                    diagnosticSummary,
                    result.RawJson));
                conversation.Add(new OllamaMessage("tool", result.DisplayText, ToolName: call.Function.Name));
                if (!succeeded && failureSummary is not null)
                {
                    progressUpdates.Add($"Tool {stepNumber}/{totalSteps} reported a failure: {failureSummary}");
                }

                completedSteps++;
            }

            var tools = await mcp.ListToolsAsOllamaToolsAsync(cancellationToken).ConfigureAwait(false);
            progressUpdates.Add($"Round {round + 1}: evaluating next plan step with {tools.Count} MCP tools available.");

            var response = await OllamaClient.ChatAsync(settings.OllamaBaseUrl, settings.Model, conversation, tools, cancellationToken).ConfigureAwait(false);
            conversation.Add(response.Message);

            if (response.Message.ToolCalls is null || response.Message.ToolCalls.Count == 0)
            {
                progressUpdates.Add("Assistant completed planning and returned a final response.");
                var assistantMessage = response.Message.Content ?? string.Empty;
                return new ChatTurnResult(
                    assistantMessage,
                    toolCalls,
                    progressUpdates,
                    assistantMessage,
                    BuildFailureDiagnostic(toolCalls));
            }

            currentCalls = response.Message.ToolCalls;
            totalSteps += currentCalls.Count;
        }

        progressUpdates.Add("Reached tool-call safety limit before assistant produced a final answer.");
        const string finalMessage = "Reached tool-call round limit before model produced a final answer.";
        return new ChatTurnResult(finalMessage, toolCalls, progressUpdates, finalMessage, BuildFailureDiagnostic(toolCalls));
    }

    public async Task<ChatTurnResult> SendAsync(string userPrompt, string? imageBase64, CancellationToken cancellationToken = default)
    {
        var plan = await ProposePlanAsync(userPrompt, imageBase64, cancellationToken).ConfigureAwait(false);
        if (!plan.RequiresApproval)
        {
            return new ChatTurnResult(
                plan.PlanSummary,
                [],
                ["Assistant completed planning and returned a final response."],
                plan.PlanSummary,
                null);
        }

        return await ExecuteApprovedPlanAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => mcp.Dispose();

    private static bool ContainsTool(IReadOnlyList<string> toolNames, string requiredTool)
    {
        foreach (var toolName in toolNames)
        {
            if (string.Equals(toolName, requiredTool, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<OllamaToolCall> EnsureTemplateEnumerationStep(List<OllamaToolCall> proposedCalls, JsonArray toolCatalog)
    {
        if (!HasTool(toolCatalog, "list_part_templates"))
        {
            return proposedCalls;
        }

        var hasCreatePart = false;
        var hasTemplateList = false;

        foreach (var call in proposedCalls)
        {
            if (string.Equals(call.Function.Name, "create_part", StringComparison.OrdinalIgnoreCase))
            {
                hasCreatePart = true;
            }

            if (string.Equals(call.Function.Name, "list_part_templates", StringComparison.OrdinalIgnoreCase))
            {
                hasTemplateList = true;
            }
        }

        if (!hasCreatePart || hasTemplateList)
        {
            return proposedCalls;
        }

        var revised = new List<OllamaToolCall>(proposedCalls.Count + 1)
        {
            new(new OllamaFunctionCall("list_part_templates", new JsonObject())),
        };

        revised.AddRange(proposedCalls);
        return revised;
    }

    private static bool HasTool(JsonArray toolCatalog, string toolName)
    {
        foreach (var toolNode in toolCatalog)
        {
            if (toolNode is not JsonObject toolObject)
            {
                continue;
            }

            if (toolObject["function"] is not JsonObject functionObject)
            {
                continue;
            }

            var name = functionObject["name"]?.GetValue<string>();
            if (string.Equals(name, toolName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly JsonDocumentOptions LenientJsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static JsonObject ToJsonObject(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            return jsonObject;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text))
        {
            return ParseJsonObjectLenient(text) ?? new JsonObject();
        }

        return new JsonObject();
    }

    private static IReadOnlyList<string> BuildPlanDiagnostics(string? assistantContent, bool parsedFromContent, IReadOnlyCollection<object>? plannedCalls)
    {
        List<string> diagnostics = [];
        diagnostics.Add(parsedFromContent
            ? "Plan source: assistant text parsed into tool calls."
            : "Plan source: direct model tool-call response.");

        if (plannedCalls is not null)
        {
            diagnostics.Add($"Detected executable tool steps: {plannedCalls.Count}.");
        }

        if (string.IsNullOrWhiteSpace(assistantContent))
        {
            diagnostics.Add("Assistant text content was empty.");
        }
        else if (parsedFromContent)
        {
            diagnostics.Add($"Assistant text length: {assistantContent.Length} characters.");
        }

        return diagnostics;
    }

    private static bool IsSuccessfulToolResult(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return false;
        }

        var trimmed = result.Trim();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try
            {
                using var json = JsonDocument.Parse(trimmed);
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (json.RootElement.TryGetProperty("success", out var successProperty)
                        && successProperty.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        return successProperty.GetBoolean();
                    }

                    if (json.RootElement.TryGetProperty("error", out var errorProperty)
                        && errorProperty.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(errorProperty.GetString()))
                    {
                        return false;
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        return !trimmed.StartsWith("Failed", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("\"success\": false", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("""success": false""", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("\"error\":", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains(" error ", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFailureSummary(string toolName, string result)
    {
        var singleLine = result.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(singleLine)
            ? $"{toolName} returned an empty failure result."
            : $"{toolName}: {singleLine}";
    }

    private static string BuildToolDiagnosticSummary(string toolName, JsonObject arguments, McpToolCallResult result, bool succeeded)
    {
        List<string> parts = [];
        parts.Add($"tool={toolName}");
        parts.Add($"status={(succeeded ? "success" : "failure")}");
        parts.Add($"args={arguments.ToJsonString()}");

        if (!string.Equals(result.DisplayText, result.RawJson, StringComparison.Ordinal))
        {
            parts.Add("responseSource=content[0].text");
        }
        else
        {
            parts.Add("responseSource=result-json");
        }

        if (!string.IsNullOrWhiteSpace(result.StderrSummary))
        {
            parts.Add($"stderr={result.StderrSummary.Replace(Environment.NewLine, " | ", StringComparison.Ordinal)}");
        }

        return string.Join("; ", parts);
    }

    private static ChatFailureDiagnostic? BuildFailureDiagnostic(IReadOnlyList<ToolExecution> toolCalls)
    {
        var failures = toolCalls.Where(call => !call.Succeeded).ToList();
        if (failures.Count == 0)
        {
            return null;
        }

        return new ChatFailureDiagnostic(
            failures.Count,
            failures.Select(call => call.FailureSummary ?? call.Name).ToList(),
            failures.Select(call => call.DiagnosticSummary ?? call.Name).ToList());
    }

    internal static List<OllamaToolCall> ParseToolCallsFromContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var toolCalls = new List<OllamaToolCall>();
        foreach (var objectText in EnumerateJsonObjects(StripMarkdownCodeFences(content)))
        {
            var json = ParseJsonObjectLenient(objectText);
            if (json is null)
            {
                continue;
            }

            var functionCall = TryParseToolCall(json);
            if (functionCall is null)
            {
                continue;
            }

            toolCalls.Add(functionCall);
        }

        return toolCalls;
    }

    private static OllamaToolCall? TryParseToolCall(JsonObject json)
    {
        var name = json["name"]?.GetValue<string>();
        var arguments = json["arguments"];

        if (string.IsNullOrWhiteSpace(name) && json["function"] is JsonObject functionObject)
        {
            name = functionObject["name"]?.GetValue<string>();
            arguments = functionObject["arguments"];
        }

        if (string.IsNullOrWhiteSpace(name) && json["function"] is JsonValue functionValue && functionValue.TryGetValue(out string? functionName))
        {
            name = functionName;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new OllamaToolCall(new OllamaFunctionCall(name, arguments?.DeepClone()));
    }

    private static string StripMarkdownCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal) || !trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return content;
        }

        var firstLineBreak = trimmed.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            return content;
        }

        var body = trimmed[(firstLineBreak + 1)..];
        var closingFenceIndex = body.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex < 0 ? content : body[..closingFenceIndex];
    }

    private static JsonObject? ParseJsonObjectLenient(string jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(jsonText, documentOptions: LenientJsonOptions) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateJsonObjects(string content)
    {
        var depth = 0;
        var start = -1;
        var inString = false;
        var escaped = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character == '{')
            {
                if (depth == 0)
                {
                    start = index;
                }

                depth++;
                continue;
            }

            if (character != '}')
            {
                continue;
            }

            if (depth == 0)
            {
                continue;
            }

            depth--;
            if (depth == 0 && start >= 0)
            {
                yield return content[start..(index + 1)];
                start = -1;
            }
        }
    }
}
