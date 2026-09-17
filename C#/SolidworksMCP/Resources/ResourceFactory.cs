namespace SolidworksMCP;

public static class ResourceFactory
{
    public static void RegisterDefaults(AppConfiguration configuration)
    {
        ResourceRegistrySingleton.Instance.Clear();

        ResourceRegistrySingleton.Instance.Register(new ResourceDefinition(
            "design-table",
            "Design Table",
            "Manages SolidWorks design tables with SQL integration",
            JsonSchemaBuilder.Any(),
            (id, name, properties) => new DesignTableResource(id, name, properties),
            new List<Dictionary<string, object?>>
            {
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["tableName"] = "ParametricBox",
                    ["parameters"] = new[]
                    {
                        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "Length", ["type"] = "dimension", ["dataType"] = "number", ["sqlColumn"] = "length" },
                        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "Width", ["type"] = "dimension", ["dataType"] = "number", ["sqlColumn"] = "width" },
                        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "Height", ["type"] = "dimension", ["dataType"] = "number", ["sqlColumn"] = "height" },
                    },
                    ["dataSource"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = "sql",
                        ["connectionString"] = "mssql://server:1433/database",
                        ["query"] = "SELECT * FROM design_configurations",
                    },
                }
            }));

        if (configuration.EnablePdm)
        {
            ResourceRegistrySingleton.Instance.Register(new ResourceDefinition(
                "pdm-configuration",
                "PDM Configuration",
                "Manages SolidWorks PDM vault configurations and operations",
                JsonSchemaBuilder.Any(),
                (id, name, properties) => new PDMResource(id, name, properties),
                new List<Dictionary<string, object?>>
                {
                    new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["vaultName"] = "Engineering",
                        ["operations"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["checkIn"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["enabled"] = true, ["comment"] = "Auto check-in" },
                            ["checkOut"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["enabled"] = true, ["getLatestVersion"] = true },
                        },
                    }
                }));
        }
    }
}
