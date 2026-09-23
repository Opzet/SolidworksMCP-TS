namespace AutoWorks.Runtime;

public sealed record SkillCatalogEntry(
    string Name,
    string Label,
    string Category,
    string Description,
    IReadOnlyList<string> Capabilities,
    string Status);

public sealed record SkillCatalogSnapshot(
    int ToolCount,
    int ResourceCount,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<SkillCatalogEntry> Skills,
    IReadOnlyList<string> EnabledSkills,
    IReadOnlyList<string> DisabledSkills,
    IReadOnlyList<string> MissingSkills);
