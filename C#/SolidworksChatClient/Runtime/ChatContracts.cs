namespace SolidworksChatClient.Runtime;

using System.Text.Json.Nodes;

internal sealed record ChatSettings(string OllamaBaseUrl, string Model, string McpCommand, string[] McpArgs);

internal sealed record ToolExecution(string Name, string Arguments, string Result);

internal sealed record ChatTurnResult(string FinalResponse, IReadOnlyList<ToolExecution> ToolCalls, IReadOnlyList<string> ProgressUpdates);

internal sealed record OllamaChatRequest(string Model, IReadOnlyList<OllamaMessage> Messages, JsonArray? Tools, bool Stream);

internal sealed record OllamaChatResponse(OllamaMessage Message);

internal sealed record OllamaConnectionStatus(
    bool IsReachable,
    bool IsModelAvailable,
    int AvailableModelCount,
    IReadOnlyList<string> AvailableModels);

internal sealed record McpConnectionStatus(int ToolCount, IReadOnlyList<string> ToolNames);

internal sealed record SolidWorksWarmupStatus(bool Attempted, bool Succeeded, string Message);

internal sealed record OllamaMessage(
    string Role,
    string? Content,
    string? ToolName = null,
    List<string>? Images = null,
    List<OllamaToolCall>? ToolCalls = null);

internal sealed record OllamaToolCall(OllamaFunctionCall Function);

internal sealed record OllamaFunctionCall(string Name, JsonNode? Arguments);
