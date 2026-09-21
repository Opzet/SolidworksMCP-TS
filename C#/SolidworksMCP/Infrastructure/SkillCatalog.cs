namespace SolidworksMCP;

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

internal static class SkillCatalog
{
    private static readonly SkillCatalogEntry[] Entries =
    [
        new("analysis", "Analysis", "toolset", "Mass properties, body/face/edge inspection, interference, and rebuild diagnostics.", ["analysis"], "enabled"),
        new("drawing", "Drawing", "toolset", "Drawing creation, sheet management, views, and section/detail workflows.", ["drawing"], "enabled"),
        new("export", "Export", "toolset", "File export, batch export, and screenshot capture workflows.", ["export"], "enabled"),
        new("modeling", "Modeling", "toolset", "Model, assembly, saving, rebuild, dimension, and document workflows.", ["modeling"], "enabled"),
        new("native-macro", "Native Macro", "toolset", "Native macro recording and playback workflows.", ["native-macro"], "enabled"),
        new("sketch", "Sketch", "toolset", "Sketch creation, editing, entities, constraints, and status inspection.", ["sketch"], "enabled"),
        new("template-manager", "Template Manager", "toolset", "Drawing template extraction, application, and storage workflows.", ["template-manager"], "enabled"),
        new("vba", "VBA", "toolset", "VBA generation and macro authoring workflows.", ["vba"], "enabled"),
        new("design-table", "Design Table", "resource", "Design table resource management, SQL-backed updates, and generated configurations.", ["design-table", "sql-integration", "auto-update"], "enabled"),
        new("pdm", "PDM", "resource", "PDM vault configuration and automation workflows.", ["pdm-integration", "pdm-workflow", "pdm-automation"], "disabled"),
        new("diagnostics", "Diagnostics", "missing", "Not yet implemented in the C# server.", ["diagnostics"], "missing"),
        new("drawing-analysis", "Drawing Analysis", "missing", "Not yet implemented in the C# server.", ["drawing-analysis"], "missing"),
        new("enhanced-drawing", "Enhanced Drawing", "missing", "Not yet implemented in the C# server.", ["enhanced-drawing"], "missing"),
        new("extrusion-helper", "Extrusion Helper", "missing", "Not yet implemented in the C# server.", ["extrusion-helper"], "missing"),
        new("macro-security", "Macro Security", "missing", "Not yet implemented in the C# server.", ["macro-security"], "missing"),
        new("vba-advanced", "VBA Advanced", "missing", "Not yet implemented in the C# server.", ["vba-advanced"], "missing"),
        new("vba-assembly", "VBA Assembly", "missing", "Not yet implemented in the C# server.", ["vba-assembly"], "missing"),
        new("vba-drawing", "VBA Drawing", "missing", "Not yet implemented in the C# server.", ["vba-drawing"], "missing"),
        new("vba-file-management", "VBA File Management", "missing", "Not yet implemented in the C# server.", ["vba-file-management"], "missing"),
        new("vba-part", "VBA Part", "missing", "Not yet implemented in the C# server.", ["vba-part"], "missing"),
    ];

    public static SkillCatalogSnapshot Build(AppConfiguration configuration, int toolCount, int resourceCount)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var skills = new List<SkillCatalogEntry>(Entries.Length);
        var enabled = new List<string>();
        var disabled = new List<string>();
        var missing = new List<string>();
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Entries)
        {
            var status = entry.Name == "pdm"
                ? (configuration.EnablePdm ? "enabled" : "disabled")
                : entry.Status;

            skills.Add(entry with { Status = status });

            if (status == "enabled")
            {
                enabled.Add(entry.Name);
                foreach (var capability in entry.Capabilities)
                {
                    capabilities.Add(capability);
                }
            }
            else if (status == "disabled")
            {
                disabled.Add(entry.Name);
            }
            else
            {
                missing.Add(entry.Name);
            }
        }

        return new SkillCatalogSnapshot(toolCount, resourceCount, [.. capabilities], skills, enabled, disabled, missing);
    }
}
