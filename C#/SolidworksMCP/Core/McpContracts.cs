namespace SolidworksMCP;

using System.Text.Json;
using System.Text.Json.Nodes;

public enum ResourceStatus
{
    Pending,
    Creating,
    Created,
    Updating,
    Deleting,
    Deleted,
    Failed,
    Executing,
    Completed,
    Unknown,
}

public sealed record ValidationIssue(string Path, string Message);

public sealed record ValidationResult
{
    public static ValidationResult Success { get; } = new(true, Array.Empty<ValidationIssue>(), Array.Empty<ValidationIssue>());

    public ValidationResult(bool valid, IReadOnlyList<ValidationIssue> errors, IReadOnlyList<ValidationIssue> warnings)
    {
        Valid = valid;
        Errors = errors;
        Warnings = warnings;
    }

    public bool Valid { get; }
    public IReadOnlyList<ValidationIssue> Errors { get; }
    public IReadOnlyList<ValidationIssue> Warnings { get; }
}

public sealed record ResourceMetadata
{
    public string CreatedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");

    public string UpdatedAt { get; init; } = DateTimeOffset.UtcNow.ToString("O");

    public string CreatedBy { get; init; } = "solidworks-mcp";

    public int Version { get; set; } = 1;

    public Dictionary<string, string> Tags { get; init; } = new();

    public Dictionary<string, string> Annotations { get; init; } = new();

    public string? SolidWorksVersion { get; init; }

    public string? FileReference { get; init; }
}

public sealed record ResourceDependency(string ResourceId, string Type, string? OutputRef = null);

public sealed record ResourceState
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string Name { get; init; }

    public required Dictionary<string, object?> Properties { get; init; }

    public required Dictionary<string, object?> Outputs { get; init; }

    public required ResourceMetadata Metadata { get; init; }

    public required ResourceStatus Status { get; init; }
}

public delegate ValueTask<object?> McpToolHandler(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken);

public delegate ValueTask<object?> McpResourceReader(string uri, ResourceStateStore stateStore, CancellationToken cancellationToken);

public sealed record McpToolDefinition(string Name, string Description, JsonNode InputSchema, McpToolHandler Handler);

public sealed record McpResourceDefinition(string Type, string Name, string Description, JsonNode Schema, Func<string, string, Dictionary<string, object?>, SolidWorksResource> Factory, IReadOnlyList<Dictionary<string, object?>>? Examples = null);

public sealed record MacroAction
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string Name { get; init; }

    public required string Timestamp { get; init; }

    public required Dictionary<string, object?> Parameters { get; init; }
}

public sealed record MacroLog
{
    public required string Timestamp { get; init; }

    public required string Level { get; init; }

    public required string Message { get; init; }

    public object? Data { get; init; }
}

public sealed record MacroRecording
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string StartTime { get; init; }

    public string? EndTime { get; set; }

    public List<MacroAction> Actions { get; init; } = new();

    public Dictionary<string, object?> Metadata { get; init; } = new();
}

public sealed record MacroExecution
{
    public required string Id { get; init; }

    public required string MacroId { get; init; }

    public required string StartTime { get; init; }

    public string? EndTime { get; set; }

    public required string Status { get; set; }

    public List<MacroLog> Logs { get; init; } = new();

    public List<object?>? Result { get; set; }

    public string? Error { get; set; }
}

public interface IStateSerializer
{
    string SerializeState(IEnumerable<ResourceState> states);

    IReadOnlyList<ResourceState> DeserializeState(string json);
}
