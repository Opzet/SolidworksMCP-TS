namespace SolidworksMCP;

using System.Text.Json;

public static class ModelingTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("open_model", "Open a SolidWorks part, assembly, or drawing file", JsonSchemaBuilder.ObjectWithRequired(["path"], ("path", JsonSchemaBuilder.String("Full path to the SolidWorks file"))), HandleOpenModel),
        new McpToolDefinition("get_active_document_info", "Get connection and active document details", JsonSchemaBuilder.Object(), HandleGetActiveDocumentInfo),
        new McpToolDefinition("save_document", "Save the active document as a new file under the managed output path", JsonSchemaBuilder.ObjectWithRequired(["path"], ("path", JsonSchemaBuilder.String("Full output path for the active SolidWorks document"))), HandleSaveDocument),
        new McpToolDefinition("save_active_document", "Save the already-saved active document in place", JsonSchemaBuilder.Object(), HandleSaveActiveDocument),
        new McpToolDefinition("create_part", "Create a new SolidWorks part document", JsonSchemaBuilder.Object(), HandleCreatePart),
        new McpToolDefinition("create_assembly", "Create a new SolidWorks assembly document", JsonSchemaBuilder.Object(), HandleCreateAssembly),
        new McpToolDefinition("list_part_templates", "List available part templates from SolidWorks defaults/settings and registry-backed locations", JsonSchemaBuilder.Object(), HandleListPartTemplates),
        new McpToolDefinition("launch_solidworks_and_check_connection", "Launch SolidWorks if needed, reuse existing instance, and bring it to front", JsonSchemaBuilder.Object(), HandleLaunchSolidWorksAndCheckConnection),
        new McpToolDefinition("close_model", "Close the current model with option to save", JsonSchemaBuilder.Object(("save", JsonSchemaBuilder.Boolean("Save before closing", false))), HandleCloseModel),
        new McpToolDefinition("list_components", "List components in the active assembly with stable handles and origins", JsonSchemaBuilder.Object(("top_level_only", JsonSchemaBuilder.Boolean("Only list top-level components", true))), HandleListComponents),
        new McpToolDefinition("insert_component", "Insert a part or sub-assembly into the active assembly", JsonSchemaBuilder.ObjectWithRequired(["path"], ("path", JsonSchemaBuilder.String("Full path to a .sldprt or .sldasm file")), ("x_mm", JsonSchemaBuilder.Number("Insertion X position in mm", 0)), ("y_mm", JsonSchemaBuilder.Number("Insertion Y position in mm", 0)), ("z_mm", JsonSchemaBuilder.Number("Insertion Z position in mm", 0)), ("configuration", JsonSchemaBuilder.String("Optional configuration name"))), HandleInsertComponent),
        new McpToolDefinition("add_mate", "Add a mate in the active assembly using declarative selection", JsonSchemaBuilder.ObjectWithRequired(["mate_type", "selection"], ("mate_type", JsonSchemaBuilder.Enum(["coincident", "concentric", "distance", "angle", "parallel", "perpendicular", "tangent", "lock"])), ("selection", JsonSchemaBuilder.Any("Declarative selection object referencing planes, axes, features, sketches, or components")), ("alignment", JsonSchemaBuilder.Enum(["closest", "aligned", "anti_aligned"], "Mate alignment", "closest")), ("distance_mm", JsonSchemaBuilder.Number("Distance for distance mates in mm", 0)), ("angle_deg", JsonSchemaBuilder.Number("Angle for angle mates in degrees", 0)), ("flip", JsonSchemaBuilder.Boolean("Flip mate alignment", false)), ("lock_rotation", JsonSchemaBuilder.Boolean("Lock rotation for applicable mates", false))), HandleAddMate),
        new McpToolDefinition("set_component_fixed", "Fix or float selected assembly components by name", JsonSchemaBuilder.ObjectWithRequired(["component_names"], ("component_names", JsonSchemaBuilder.Array(JsonSchemaBuilder.String(), "Component names to update")), ("fixed", JsonSchemaBuilder.Boolean("True to fix, false to float", true))), HandleSetComponentFixed),
        new McpToolDefinition("create_extrusion", "Create an extrusion feature", JsonSchemaBuilder.ObjectWithRequired(["depth"], ("depth", JsonSchemaBuilder.Number("Extrusion depth in mm")), ("draft", JsonSchemaBuilder.Number("Draft angle in degrees", 0)), ("reverse", JsonSchemaBuilder.Boolean("Reverse direction", false))), HandleCreateExtrusion),
        new McpToolDefinition("get_dimension", "Get the value of a dimension", JsonSchemaBuilder.ObjectWithRequired(["name"], ("name", JsonSchemaBuilder.String("Dimension name (e.g. D1@Sketch1)"))), HandleGetDimension),
        new McpToolDefinition("set_dimension", "Set the value of a dimension", JsonSchemaBuilder.ObjectWithRequired(["name", "value"], ("name", JsonSchemaBuilder.String("Dimension name (e.g. D1@Sketch1)")), ("value", JsonSchemaBuilder.Number("New value in mm"))), HandleSetDimension),
        new McpToolDefinition("rebuild_model", "Rebuild the current model", JsonSchemaBuilder.Object(("force", JsonSchemaBuilder.Boolean("Force rebuild even if not needed", false))), HandleRebuildModel),
        new McpToolDefinition("get_rebuild_status", "Rebuild the active document and report error and warning status for planning workflows", JsonSchemaBuilder.Object(("force", JsonSchemaBuilder.Boolean("Force rebuild even if not needed", false))), HandleGetRebuildStatus),
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

    private static ValueTask<object?> HandleGetActiveDocumentInfo(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.GetActiveDocumentInfo()));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to get active document info: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleSaveDocument(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var savedPath = api.SaveDocument(ToolHelpers.GetString(args, "path"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Saved document: {savedPath}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to save document: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleSaveActiveDocument(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            var savedPath = api.SaveActiveDocument();
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Saved active document: {savedPath}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to save active document: {ex.Message}"));
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

    private static ValueTask<object?> HandleCreateAssembly(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            var model = api.CreateAssembly();
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["message"] = $"Created new assembly: {model.Name}",
                ["name"] = model.Name,
                ["type"] = model.Type,
                ["templatePath"] = model.TemplatePath ?? "<SolidWorks default>",
                ["templateSource"] = model.TemplateSource ?? "unknown",
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to create assembly: {ex.Message}"));
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

    private static ValueTask<object?> HandleListComponents(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["components"] = api.ListComponents(ToolHelpers.GetBool(args, "top_level_only", true)),
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to list components: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleInsertComponent(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var result = api.InsertComponent(
                ToolHelpers.GetString(args, "path"),
                ToolHelpers.GetDouble(args, "x_mm"),
                ToolHelpers.GetDouble(args, "y_mm"),
                ToolHelpers.GetDouble(args, "z_mm"),
                ToolHelpers.GetString(args, "configuration"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to insert component: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddMate(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var result = api.AddMate(
                ToolHelpers.GetSelection(args),
                ToolHelpers.GetString(args, "mate_type"),
                ToolHelpers.GetString(args, "alignment", "closest"),
                ToolHelpers.GetDouble(args, "distance_mm"),
                ToolHelpers.GetDouble(args, "angle_deg"),
                ToolHelpers.GetBool(args, "flip"),
                ToolHelpers.GetBool(args, "lock_rotation"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add mate: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleSetComponentFixed(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var result = api.SetComponentFixed(ToolHelpers.GetStringList(args, "component_names"), ToolHelpers.GetBool(args, "fixed", true));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to set component fixed state: {ex.Message}"));
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

    private static ValueTask<object?> HandleGetRebuildStatus(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var result = api.GetRebuildStatus(ToolHelpers.GetBool(args, "force"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to get rebuild status: {ex.Message}"));
        }
    }
}
