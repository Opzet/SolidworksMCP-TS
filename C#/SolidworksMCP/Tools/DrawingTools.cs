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
        new McpToolDefinition("add_section_view", "Add a section view to the drawing", JsonSchemaBuilder.ObjectWithRequired(["parentView", "x", "y", "sectionLine"], ("parentView", JsonSchemaBuilder.String()), ("x", JsonSchemaBuilder.Number()), ("y", JsonSchemaBuilder.Number()), ("sectionLine", JsonSchemaBuilder.Any())), HandleAddSectionView),
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

    private static ValueTask<object?> HandleAddSectionView(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var drawingDoc = api.GetCurrentModel() ?? throw new InvalidOperationException("Current document must be a drawing");

            var parentViewName = ToolHelpers.GetString(args, "parentView");
            var xMeters = ToolHelpers.GetDouble(args, "x") / 1000d;
            var yMeters = ToolHelpers.GetDouble(args, "y") / 1000d;

            var sectionLine = ToolHelpers.GetDictionary(args, "sectionLine");
            var x1 = ToolHelpers.GetDouble(sectionLine, "x1", 0) / 1000d;
            var y1 = ToolHelpers.GetDouble(sectionLine, "y1", 0) / 1000d;
            var x2 = ToolHelpers.GetDouble(sectionLine, "x2", 0) / 1000d;
            var y2 = ToolHelpers.GetDouble(sectionLine, "y2", 0) / 1000d;
            var label = ToolHelpers.GetString(sectionLine, "label", "A-A");

            var extension = drawingDoc.GetType().GetProperty("Extension")?.GetValue(drawingDoc);
            if (extension is null)
            {
                throw new InvalidOperationException("Drawing extension is unavailable.");
            }

            var parentSelected = extension.GetType().GetMethod("SelectByID2")?.Invoke(extension, [parentViewName, "DRAWINGVIEW", 0d, 0d, 0d, false, 0, null, 0]) is bool selected && selected;
            if (!parentSelected)
            {
                throw new InvalidOperationException($"Parent view '{parentViewName}' could not be selected.");
            }

            object? sectionView = null;
            sectionView = drawingDoc.GetType().GetMethod("CreateSectionViewAt5")?.Invoke(drawingDoc, [xMeters, yMeters, x1, y1, x2, y2, 0, label, 0, false])
                ?? drawingDoc.GetType().GetMethod("CreateSectionViewAt4")?.Invoke(drawingDoc, [xMeters, yMeters, x1, y1, x2, y2, 0, label, 0])
                ?? drawingDoc.GetType().GetMethod("CreateSectionViewAt3")?.Invoke(drawingDoc, [xMeters, yMeters, x1, y1, x2, y2, 0, label]);

            drawingDoc.GetType().GetMethod("ClearSelection2")?.Invoke(drawingDoc, [true]);

            if (sectionView is null)
            {
                throw new InvalidOperationException("SolidWorks did not create a section view with the supplied line and insertion point.");
            }

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["created"] = true,
                ["parentView"] = parentViewName,
                ["sectionLabel"] = label,
                ["xMm"] = ToolHelpers.GetDouble(args, "x"),
                ["yMm"] = ToolHelpers.GetDouble(args, "y"),
            }));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add section view: {ex.Message}"));
        }
    }
}
