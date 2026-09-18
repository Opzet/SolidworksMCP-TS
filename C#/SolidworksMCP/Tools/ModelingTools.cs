namespace SolidworksMCP;

using System.Text.Json;

public static class ModelingTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("open_model", "Open a SolidWorks part, assembly, or drawing file", JsonSchemaBuilder.ObjectWithRequired(["path"], ("path", JsonSchemaBuilder.String("Full path to the SolidWorks file"))), HandleOpenModel),
        new McpToolDefinition("create_part", "Create a new SolidWorks part document", JsonSchemaBuilder.Object(), HandleCreatePart),
        new McpToolDefinition("list_part_templates", "List available part templates from SolidWorks defaults/settings and registry-backed locations", JsonSchemaBuilder.Object(), HandleListPartTemplates),
        new McpToolDefinition("launch_solidworks_and_check_connection", "Launch SolidWorks if needed, reuse existing instance, and bring it to front", JsonSchemaBuilder.Object(), HandleLaunchSolidWorksAndCheckConnection),
        new McpToolDefinition("close_model", "Close the current model with option to save", JsonSchemaBuilder.Object(("save", JsonSchemaBuilder.Boolean("Save before closing", false))), HandleCloseModel),
        new McpToolDefinition("create_extrusion", "Create an extrusion feature", JsonSchemaBuilder.ObjectWithRequired(["depth"], ("depth", JsonSchemaBuilder.Number("Extrusion depth in mm")), ("draft", JsonSchemaBuilder.Number("Draft angle in degrees", 0)), ("reverse", JsonSchemaBuilder.Boolean("Reverse direction", false))), HandleCreateExtrusion),
        new McpToolDefinition("get_dimension", "Get the value of a dimension", JsonSchemaBuilder.ObjectWithRequired(["name"], ("name", JsonSchemaBuilder.String("Dimension name (e.g. D1@Sketch1)"))), HandleGetDimension),
        new McpToolDefinition("set_dimension", "Set the value of a dimension", JsonSchemaBuilder.ObjectWithRequired(["name", "value"], ("name", JsonSchemaBuilder.String("Dimension name (e.g. D1@Sketch1)")), ("value", JsonSchemaBuilder.Number("New value in mm"))), HandleSetDimension),
        new McpToolDefinition("rebuild_model", "Rebuild the current model", JsonSchemaBuilder.Object(("force", JsonSchemaBuilder.Boolean("Force rebuild even if not needed", false))), HandleRebuildModel),
    ];

    private static ValueTask<object?> HandleOpenModel(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var model = api.OpenModel(ToolHelpers.GetString(args, "path"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Opened {model.Type}: {model.Name}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to open model: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCreatePart(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            var model = api.CreatePart();
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["message"] = $"Created new part: {model.Name}",
                ["name"] = model.Name,
                ["type"] = model.Type,
                ["templatePath"] = model.TemplatePath ?? "<SolidWorks default>",
                ["templateSource"] = model.TemplateSource ?? "unknown",
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to create part: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleLaunchSolidWorksAndCheckConnection(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;

        try
        {
            var message = api.LaunchAndBringToFront();
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText(message));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to launch SolidWorks and check connection: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleListPartTemplates(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;

        try
        {
            var templates = api.ListPartTemplates();
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(templates));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to list part templates: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCloseModel(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var currentModel = api.GetCurrentModel();
            if (currentModel is null)
            {
                return ValueTask.FromResult<object?>(ToolHelpers.SuccessText("No active model to close"));
            }

            var title = api.GetCurrentModelTitleOrPath();

            api.CloseModel(ToolHelpers.GetBool(args, "save"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Model \"{title}\" closed successfully"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to close model: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCreateExtrusion(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var feature = api.CreateExtrude(ToolHelpers.GetDouble(args, "depth"), ToolHelpers.GetDouble(args, "draft"), ToolHelpers.GetBool(args, "reverse"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Created extrusion: {feature.Name}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to create extrusion: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleGetDimension(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var value = api.GetDimension(ToolHelpers.GetString(args, "name"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Dimension {ToolHelpers.GetString(args, "name")} = {value} mm"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to get dimension: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleSetDimension(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            api.SetDimension(ToolHelpers.GetString(args, "name"), ToolHelpers.GetDouble(args, "value"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Set dimension {ToolHelpers.GetString(args, "name")} = {ToolHelpers.GetDouble(args, "value")} mm"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to set dimension: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleRebuildModel(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var result = api.RebuildModel(ToolHelpers.GetBool(args, "force"));
            return ValueTask.FromResult<object?>(result);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to rebuild model: {ex.Message}"));
        }
    }
}
