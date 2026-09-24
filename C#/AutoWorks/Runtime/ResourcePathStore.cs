namespace AutoWorks.Runtime;

using System.Text.Json;

internal static class ResourcePathStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string StateFilePath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".solidworks-mcp-state.json"));

    public static async Task<ResourcePathSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(StateFilePath))
        {
            return CreateSnapshot([]);
        }

        await using var stream = File.OpenRead(StateFilePath);
        var snapshot = await JsonSerializer.DeserializeAsync<ResourcePathSnapshot>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return snapshot ?? CreateSnapshot([]);
    }

    public static async Task SaveAsync(IReadOnlyList<ResourcePathEntry> resources, CancellationToken cancellationToken = default)
    {
        var snapshot = CreateSnapshot(resources);
        Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath)!);

        await using var stream = File.Create(StateFilePath);
        await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static ResourcePathSnapshot CreateSnapshot(IReadOnlyList<ResourcePathEntry> resources)
    {
        var byType = resources
            .GroupBy(resource => resource.Kind, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var byStatus = resources
            .GroupBy(resource => resource.Status, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new ResourcePathSnapshot(
            "1.0.0",
            DateTimeOffset.UtcNow,
            resources,
            new ResourcePathMetadata(resources.Count, byType, byStatus));
    }
}