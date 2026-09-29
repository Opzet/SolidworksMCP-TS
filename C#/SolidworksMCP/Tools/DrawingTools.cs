namespace SolidworksMCP;

using System.Text.Json;

public static class DrawingTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("create_drawing_from_model", "Create a new drawing from the current 3D model", JsonSchemaBuilder.Object(("template", JsonSchemaBuilder.String("Drawing template path")), ("sheet_size", JsonSchemaBuilder.Enum(["A4", "A3", "A2", "A1", "A0", "Letter", "Tabloid"], "Sheet size"))), HandleCreateDrawingFromModel),
        new McpToolDefinition("create_drawing", "Create a new drawing from the current 3D model", JsonSchemaBuilder.Object(("template", JsonSchemaBuilder.String("Drawing template path"))), HandleCreateDrawingFromModel),
        new McpToolDefinition("list_sheets", "List sheets in the active drawing", JsonSchemaBuilder.Object(), HandleListSheets),
        new McpToolDefinition("add_sheet", "Add a sheet to the active drawing", JsonSchemaBuilder.ObjectWithRequired(["name"], ("name", JsonSchemaBuilder.String("Sheet name")), ("paper_size", JsonSchemaBuilder.Enum(["A4", "A3", "A2", "A1", "A0"], "Paper size", "A4"))), HandleAddSheet),
        new McpToolDefinition("activate_sheet", "Activate a drawing sheet by name", JsonSchemaBuilder.ObjectWithRequired(["sheet_name"], ("sheet_name", JsonSchemaBuilder.String("Sheet name"))), HandleActivateSheet),
        new McpToolDefinition("add_drawing_view", "Add a view to the current drawing", JsonSchemaBuilder.ObjectWithRequired(["viewType", "x", "y"], ("viewType", JsonSchemaBuilder.Enum(["front", "top", "right", "back", "bottom", "left", "iso", "current"])), ("modelPath", JsonSchemaBuilder.String()), ("x", JsonSchemaBuilder.Number()), ("y", JsonSchemaBuilder.Number()), ("scale", JsonSchemaBuilder.Number("View scale", 1))), HandleAddDrawingView),
        new McpToolDefinition("insert_model_view", "Add a model view to the current drawing", JsonSchemaBuilder.ObjectWithRequired(["viewType", "x", "y"], ("viewType", JsonSchemaBuilder.Enum(["front", "top", "right", "back", "bottom", "left", "iso", "current"])), ("modelPath", JsonSchemaBuilder.String()), ("x", JsonSchemaBuilder.Number()), ("y", JsonSchemaBuilder.Number()), ("scale", JsonSchemaBuilder.Number("View scale", 1))), HandleAddDrawingView),
        new McpToolDefinition("list_drawing_views", "List views in the active drawing", JsonSchemaBuilder.Object(), HandleListDrawingViews),
        new McpToolDefinition("activate_drawing_view", "Activate a drawing view by name", JsonSchemaBuilder.ObjectWithRequired(["view_name"], ("view_name", JsonSchemaBuilder.String("Drawing view name"))), HandleActivateDrawingView),
        new McpToolDefinition("set_drawing_view", "Activate or inspect a drawing view by name", JsonSchemaBuilder.ObjectWithRequired(["viewName"], ("viewName", JsonSchemaBuilder.String("Drawing view name"))), HandleSetDrawingView),
        new McpToolDefinition("get_drawing_capabilities", "Report drawing API capability and fallback strategy for reliable automation", JsonSchemaBuilder.Object(), HandleGetDrawingCapabilities),
        new McpToolDefinition("add_section_view", "Add a section view to the drawing", JsonSchemaBuilder.ObjectWithRequired(["parentView", "x", "y", "sectionLine"], ("parentView", JsonSchemaBuilder.String()), ("x", JsonSchemaBuilder.Number()), ("y", JsonSchemaBuilder.Number()), ("sectionLine", JsonSchemaBuilder.Any()), ("fallback_macro", JsonSchemaBuilder.Any("Optional macro fallback: {macro_path,module_name,procedure_name}"))), HandleAddSectionView),
    ];

    private static ValueTask<object?> HandleCreateDrawingFromModel(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.CreateDrawingFromCurrentModel(ToolHelpers.GetString(args, "template"));
            return ValueTask.FromResult<object?>(result);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to create drawing: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleListSheets(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sheets"] = api.ListSheets(),
            }));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to list sheets: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddSheet(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.AddSheet(ToolHelpers.GetString(args, "name"), ToolHelpers.GetString(args, "paper_size", "A4"))));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add sheet: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleActivateSheet(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.ActivateSheet(ToolHelpers.GetString(args, "sheet_name"))));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to activate sheet: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddDrawingView(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.AddDrawingView(args);
            return ValueTask.FromResult<object?>(result);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add drawing view: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleListDrawingViews(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["views"] = api.ListDrawingViews(),
            }));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to list drawing views: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleActivateDrawingView(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.ActivateDrawingView(ToolHelpers.GetString(args, "view_name"))));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to activate drawing view: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleSetDrawingView(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.SetDrawingView(args)));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to set drawing view: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleGetDrawingCapabilities(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.GetDrawingCapabilities()));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to get drawing capabilities: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddSectionView(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var sectionLine = ToolHelpers.GetDictionary(args, "sectionLine");
            var fallbackMacro = ToolHelpers.GetDictionary(args, "fallback_macro");
            var result = api.AddSectionView(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentView"] = ToolHelpers.GetString(args, "parentView"),
                ["xMm"] = ToolHelpers.GetDouble(args, "x"),
                ["yMm"] = ToolHelpers.GetDouble(args, "y"),
                ["x1Mm"] = ToolHelpers.GetDouble(sectionLine, "x1", 0),
                ["y1Mm"] = ToolHelpers.GetDouble(sectionLine, "y1", 0),
                ["x2Mm"] = ToolHelpers.GetDouble(sectionLine, "x2", 0),
                ["y2Mm"] = ToolHelpers.GetDouble(sectionLine, "y2", 0),
                ["label"] = ToolHelpers.GetString(sectionLine, "label", "A-A"),
                ["fallbackMacroPath"] = ToolHelpers.GetString(fallbackMacro, "macro_path"),
                ["fallbackModuleName"] = ToolHelpers.GetString(fallbackMacro, "module_name", "main"),
                ["fallbackProcedureName"] = ToolHelpers.GetString(fallbackMacro, "procedure_name", "main"),
            });
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add section view: {ex.Message}"));
        }
    }
}
