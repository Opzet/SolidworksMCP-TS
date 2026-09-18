namespace SolidworksChatClient.Runtime;

using System.Text.Json.Nodes;

internal sealed record ChatSettings(string OllamaBaseUrl, string Model, string McpCommand, string[] McpArgs);

internal sealed record ToolExecution(
    int StepNumber,
    int TotalSteps,
    string Name,
    string Arguments,
    string Result,
    bool Succeeded,
    string? FailureSummary,
    string? DiagnosticSummary,
    string? RawResponse = null,
    string? FeedbackImagePath = null,
    string? FeedbackMediaType = null);

internal sealed record PlannedToolCall(string Name, string Arguments, string Source = "tool-call");

internal sealed record ChatPlanProposal(
    string PlanSummary,
    IReadOnlyList<PlannedToolCall> PlannedToolCalls,
    bool RequiresApproval,
    string ParseSource = "tool-call",
    IReadOnlyList<string>? Diagnostics = null);

internal sealed record ChatFailureDiagnostic(
    int FailedToolCount,
    IReadOnlyList<string> FailureSummaries,
    IReadOnlyList<string> DiagnosticDetails);

internal sealed record ChatTurnResult(
    string FinalResponse,
    IReadOnlyList<ToolExecution> ToolCalls,
    IReadOnlyList<string> ProgressUpdates,
    string FinalAssistantMessage,
    ChatFailureDiagnostic? FailureDiagnostic = null,
    string? FeedbackImagePath = null,
    string? FeedbackMediaType = null);

internal sealed record OllamaChatRequest(string Model, IReadOnlyList<OllamaMessage> Messages, JsonArray? Tools, bool Stream);

internal sealed record OllamaChatResponse(OllamaMessage Message);

internal sealed record OllamaConnectionStatus(
    bool IsReachable,
    bool IsModelAvailable,
    int AvailableModelCount,
    IReadOnlyList<string> AvailableModels);

internal sealed record McpConnectionStatus(int ToolCount, IReadOnlyList<string> ToolNames);

internal sealed record McpToolCallResult(
    string DisplayText,
    string RawJson,
    string? StderrSummary = null,
    JsonObject? StructuredData = null,
    string? FeedbackImagePath = null,
    string? FeedbackMediaType = null);

internal sealed record SolidWorksWarmupStatus(bool Attempted, bool Succeeded, string Message);

internal sealed record OllamaMessage(
    string Role,
    string? Content,
    string? ToolName = null,
    List<string>? Images = null,
    List<OllamaToolCall>? ToolCalls = null);

internal sealed record OllamaToolCall(OllamaFunctionCall Function);

internal sealed record OllamaFunctionCall(string Name, JsonNode? Arguments);
