namespace SolidworksMCP;

using System.Text.Json.Nodes;

public sealed record ResourceDefinition(
    string Type,
    string Name,
    string Description,
    JsonNode Schema,
    Func<string, string, Dictionary<string, object?>, SolidWorksResource> Factory,
    IReadOnlyList<Dictionary<string, object?>>? Examples = null);

public sealed class ResourceRegistry
{
    private readonly Dictionary<string, ResourceDefinition> resources = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ResourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (resources.ContainsKey(definition.Type))
        {
            throw new InvalidOperationException($"Resource type '{definition.Type}' is already registered");
        }

        resources[definition.Type] = definition;
    }

    public ResourceDefinition? Get(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return resources.TryGetValue(type, out var definition) ? definition : null;
    }

    public IReadOnlyList<string> GetAllTypes() => resources.Keys.ToList();

    public IReadOnlyList<ResourceDefinition> GetAllDefinitions() => resources.Values.ToList();

    public SolidWorksResource CreateResource(string type, string id, string name, Dictionary<string, object?> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(properties);

        var definition = resources.TryGetValue(type, out var value) ? value : null;
        if (definition is null)
        {
            throw new InvalidOperationException($"Unknown resource type: {type}");
        }

        return definition.Factory(id, name, properties);
    }

    public ValidationResult ValidateProperties(string type, Dictionary<string, object?> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(properties);

        var definition = Get(type);
        if (definition is null)
        {
            return new ValidationResult(false, [new ValidationIssue(string.Empty, $"Unknown resource type: {type}")], Array.Empty<ValidationIssue>());
        }

        return ValidationResult.Success;
    }

    public JsonNode? GetSchema(string type) => Get(type)?.Schema;

    public IReadOnlyList<Dictionary<string, object?>>? GetExamples(string type) => Get(type)?.Examples;

    public void Clear() => resources.Clear();
}

public static class ResourceRegistrySingleton
{
    public static ResourceRegistry Instance { get; } = new();
}
