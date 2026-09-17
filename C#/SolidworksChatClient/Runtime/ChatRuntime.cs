namespace SolidworksChatClient.Runtime;

using System.Text.Json.Nodes;

internal sealed class ChatRuntime : IDisposable
{
    private readonly ChatSettings settings;
    private readonly McpClient mcp;
    private readonly List<OllamaMessage> conversation =
    [
        new("system", "You are a SolidWorks CAD assistant. Use MCP tools whenever CAD actions are required. Be deterministic. For part creation workflows, prefer create_part, create_sketch, add_line/add_circle/add_rectangle, exit_sketch, create_extrusion, set_dimension, rebuild_model, create_drawing_from_model, and add_drawing_view. If a required capability is unavailable, clearly state the limitation and continue with available tools.")
    ];

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
            return new SolidWorksWarmupStatus(true, true, launchResult);
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
            closeNote = $" Temporary model cleanup: {closeResult}";
        }

        return new SolidWorksWarmupStatus(true, true, $"SolidWorks warm-up via create_part completed. {createResult}{closeNote}");
    }

    public async Task<ChatTurnResult> SendAsync(string userPrompt, string? imageBase64, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(userPrompt));
        }

        var userMessage = string.IsNullOrWhiteSpace(imageBase64)
            ? new OllamaMessage("user", userPrompt)
            : new OllamaMessage("user", userPrompt, Images: [imageBase64]);

        conversation.Add(userMessage);

        const int maxRounds = 8;
        var toolCalls = new List<ToolExecution>();
        var progressUpdates = new List<string>();

        for (var round = 0; round < maxRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tools = await mcp.ListToolsAsOllamaToolsAsync(cancellationToken).ConfigureAwait(false);
            progressUpdates.Add($"Round {round + 1}: evaluating next plan step with {tools.Count} MCP tools available.");

            var response = await OllamaClient.ChatAsync(settings.OllamaBaseUrl, settings.Model, conversation, tools, cancellationToken).ConfigureAwait(false);
            conversation.Add(response.Message);

            if (response.Message.ToolCalls is null || response.Message.ToolCalls.Count == 0)
            {
                progressUpdates.Add("Assistant completed planning and returned a final response.");
                return new ChatTurnResult(response.Message.Content ?? string.Empty, toolCalls, progressUpdates);
            }

            foreach (var call in response.Message.ToolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progressUpdates.Add($"Executing tool: {call.Function.Name}");

                var argsObject = ToJsonObject(call.Function.Arguments);
                var result = await mcp.CallToolAsync(call.Function.Name, argsObject, cancellationToken).ConfigureAwait(false);

                toolCalls.Add(new ToolExecution(call.Function.Name, argsObject.ToJsonString(), result));
                conversation.Add(new OllamaMessage("tool", result, ToolName: call.Function.Name));
            }
        }

        progressUpdates.Add("Reached tool-call safety limit before assistant produced a final answer.");
        return new ChatTurnResult("Reached tool-call round limit before model produced a final answer.", toolCalls, progressUpdates);
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

    private static JsonObject ToJsonObject(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            return jsonObject;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text))
        {
            return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }

        return new JsonObject();
    }
}
