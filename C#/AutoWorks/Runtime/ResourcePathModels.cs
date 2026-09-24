namespace AutoWorks.Runtime;

public sealed record ResourcePathEntry(
    string Kind,
    string Name,
    string Path,
    string Source = "list_part_templates",
    string Status = "available");

public sealed record ResourcePathMetadata(
    int TotalResources,
    IReadOnlyDictionary<string, int> ByType,
    IReadOnlyDictionary<string, int> ByStatus);

public sealed record ResourcePathSnapshot(
    string Version,
    DateTimeOffset Timestamp,
    IReadOnlyList<ResourcePathEntry> Resources,
    ResourcePathMetadata Metadata);