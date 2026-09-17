namespace SolidworksMCP;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

public abstract class SolidWorksResource
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    protected Dictionary<string, object?> Properties { get; set; }

    protected Dictionary<string, object?> Outputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    protected List<ResourceDependency> Dependencies { get; } = new();

    protected ResourceStatus Status { get; set; } = ResourceStatus.Pending;

    protected ResourceMetadata Metadata { get; set; }

    public abstract string Type { get; }

    public virtual JsonNode Schema => JsonSchemaBuilder.Any();

    [SetsRequiredMembers]
    protected SolidWorksResource(string id, string name, Dictionary<string, object?> properties)
    {
        Id = id;
        Name = name;
        Properties = JsonHelpers.NormalizeDictionary(properties);
        Metadata = new ResourceMetadata();
    }

    public virtual ValidationResult Validate()
    {
        return ValidationResult.Success;
    }

    public ResourceState ToState()
    {
        return new ResourceState
        {
            Id = Id,
            Type = Type,
            Name = Name,
            Properties = new Dictionary<string, object?>(Properties, StringComparer.OrdinalIgnoreCase),
            Outputs = new Dictionary<string, object?>(Outputs, StringComparer.OrdinalIgnoreCase),
            Metadata = Metadata,
            Status = Status,
        };
    }

    public virtual void FromState(ResourceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Properties = new Dictionary<string, object?>(state.Properties, StringComparer.OrdinalIgnoreCase);
        Outputs = new Dictionary<string, object?>(state.Outputs, StringComparer.OrdinalIgnoreCase);
        Metadata = state.Metadata;
        Status = state.Status;
    }

    public IReadOnlyList<ResourceDependency> GetDependencies() => Dependencies;

    public void AddDependency(ResourceDependency dependency) => Dependencies.Add(dependency);

    public IReadOnlyDictionary<string, object?> GetOutputs() => Outputs;

    public void SetOutputs(Dictionary<string, object?> outputs)
    {
        Outputs = outputs;
        Metadata = Metadata with { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") };
    }

    public ResourceStatus GetStatus() => Status;

    public void SetStatus(ResourceStatus status)
    {
        Status = status;
        Metadata = Metadata with { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") };
    }

    public IReadOnlyDictionary<string, object?> GetProperties() => Properties;

    public virtual ValidationResult UpdateProperties(Dictionary<string, object?> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        foreach (var pair in properties)
        {
            Properties[pair.Key] = pair.Value;
        }

        Metadata = Metadata with
        {
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Version = Metadata.Version + 1,
        };

        return ValidationResult.Success;
    }

    public abstract ValueTask<object?> ExecuteAsync(SolidWorksApi api, CancellationToken cancellationToken = default);

    public abstract string ToVBACode();

    public abstract string ToMacroCode();

    public abstract IReadOnlyList<string> GetRequiredCapabilities();
}
