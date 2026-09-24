namespace AutoWorks.Runtime;

using System.Text.Json.Nodes;

public sealed record ChatSettings(
    string OllamaBaseUrl,
    string Model,
    string McpCommand,
    string[] McpArgs,
    IReadOnlyList<ResourcePathEntry>? ResourcePaths = null);

public sealed record ToolExecution(
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

public sealed record PlannedToolCall(string Name, string Arguments, string Source = "tool-call");

public sealed record ChatPlanProposal(
    string PlanSummary,
    IReadOnlyList<PlannedToolCall> PlannedToolCalls,
    bool RequiresApproval,
    string ParseSource = "tool-call",
    IReadOnlyList<string>? Diagnostics = null);

public sealed record ChatFailureDiagnostic(
    int FailedToolCount,
    IReadOnlyList<string> FailureSummaries,
    IReadOnlyList<string> DiagnosticDetails);

public sealed record ChatTurnResult(
    string FinalResponse,
    IReadOnlyList<ToolExecution> ToolCalls,
    IReadOnlyList<string> ProgressUpdates,
    string FinalAssistantMessage,
    ChatFailureDiagnostic? FailureDiagnostic = null,
    string? FeedbackImagePath = null,
    string? FeedbackMediaType = null,
    bool StoppedOnFirstFailure = false,
    bool AwaitingHumanApproval = false,
    string? HumanApprovalPrompt = null);

public sealed record OllamaChatRequest(string Model, IReadOnlyList<OllamaMessage> Messages, JsonArray? Tools, bool Stream);

public sealed record OllamaChatResponse(OllamaMessage Message);

public sealed record OllamaConnectionStatus(
    bool IsReachable,
    bool IsModelAvailable,
    int AvailableModelCount,
    IReadOnlyList<string> AvailableModels);

public sealed record McpConnectionStatus(int ToolCount, IReadOnlyList<string> ToolNames);

public sealed record McpToolCallResult(
    string DisplayText,
    string RawJson,
    string? StderrSummary = null,
    JsonObject? StructuredData = null,
    string? FeedbackImagePath = null,
    string? FeedbackMediaType = null);

public sealed record McpToolDescriptor(string Name, string Description, JsonNode? InputSchema);

public sealed record SolidWorksWarmupStatus(bool Attempted, bool Succeeded, string Message);

public sealed record OllamaMessage(
    string Role,
    string? Content,
    string? ToolName = null,
    List<string>? Images = null,
    List<OllamaToolCall>? ToolCalls = null);

public sealed record OllamaToolCall(OllamaFunctionCall Function);

public sealed record OllamaFunctionCall(string Name, JsonNode? Arguments);
