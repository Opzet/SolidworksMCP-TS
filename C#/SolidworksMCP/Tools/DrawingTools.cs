namespace SolidworksMCP;

using System.Text.Json;

public static class DrawingTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("create_drawing_from_model", "Create a new drawing from the current 3D model", JsonSchemaBuilder.Object(("template", JsonSchemaBuilder.String("Drawing template path")), ("sheet_size", JsonSchemaBuilder.Enum(["A4", "A3", "A2", "A1", "A0", "Letter", "Tabloid"], "Sheet size"))), HandleCreateDrawingFromModel),
        new McpToolDefinition("add_drawing_view", "Add a view to the current drawing", JsonSchemaBuilder.ObjectWithRequired(["viewType", "x", "y"], ("viewType", JsonSchemaBuilder.Enum(["front", "top", "right", "back", "bottom", "left", "iso", "current"])), ("modelPath", JsonSchemaBuilder.String()), ("x", JsonSchemaBuilder.Number()), ("y", JsonSchemaBuilder.Number()), ("scale", JsonSchemaBuilder.Number("View scale", 1))), HandleAddDrawingView),
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
