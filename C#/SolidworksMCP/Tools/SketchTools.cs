namespace SolidworksMCP;

using System.Text.Json;

public static class SketchTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("create_sketch", "Create a new sketch on a specified plane or face", JsonSchemaBuilder.Object(
            ("plane", JsonSchemaBuilder.Enum(["Front", "Top", "Right", "Custom"], "Reference plane for sketch", "Front")),
            ("offset", JsonSchemaBuilder.Number("Offset distance from plane in mm", 0)),
            ("reverse", JsonSchemaBuilder.Boolean("Reverse offset direction", false)),
            ("customPlane", JsonSchemaBuilder.Any("Custom plane definition"))), HandleCreateSketch),
        new McpToolDefinition("add_line", "Add a line to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x1", "y1", "x2", "y2"], ("x1", JsonSchemaBuilder.Number()), ("y1", JsonSchemaBuilder.Number()), ("z1", JsonSchemaBuilder.Number("Z start", 0)), ("x2", JsonSchemaBuilder.Number()), ("y2", JsonSchemaBuilder.Number()), ("z2", JsonSchemaBuilder.Number("Z end", 0))), HandleAddLine),
        new McpToolDefinition("add_circle", "Add a circle to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["centerX", "centerY", "radius"], ("centerX", JsonSchemaBuilder.Number()), ("centerY", JsonSchemaBuilder.Number()), ("centerZ", JsonSchemaBuilder.Number("Center Z", 0)), ("radius", JsonSchemaBuilder.Number())), HandleAddCircle),
        new McpToolDefinition("add_rectangle", "Add a rectangle to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x1", "y1", "x2", "y2"], ("x1", JsonSchemaBuilder.Number()), ("y1", JsonSchemaBuilder.Number()), ("x2", JsonSchemaBuilder.Number()), ("y2", JsonSchemaBuilder.Number())), HandleAddRectangle),
        new McpToolDefinition("exit_sketch", "Exit sketch edit mode and rebuild", JsonSchemaBuilder.Object(("rebuild", JsonSchemaBuilder.Boolean("Rebuild model after exiting sketch", true))), HandleExitSketch),
    ];

    private static ValueTask<object?> HandleCreateSketch(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No active model");
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.CreateSketch(args);
            return ValueTask.FromResult<object?>(result);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to create sketch: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddLine(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.AddLine(args);
            return ValueTask.FromResult<object?>(result);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add line: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddCircle(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No active model");
            var sketchManager = model.GetType().GetProperty("SketchManager")?.GetValue(model);
            if (sketchManager is null)
            {
                throw new InvalidOperationException("SketchManager unavailable");
            }

            var result = sketchManager.GetType().GetMethod("CreateCircle")?.Invoke(sketchManager, [ToolHelpers.GetDouble(args, "centerX") / 1000d, ToolHelpers.GetDouble(args, "centerY") / 1000d, ToolHelpers.GetDouble(args, "centerZ") / 1000d, ToolHelpers.GetDouble(args, "radius") / 1000d]);
            if (result is null)
            {
                result = sketchManager.GetType().GetMethod("CreateCircle2")?.Invoke(sketchManager, [ToolHelpers.GetDouble(args, "centerX") / 1000d, ToolHelpers.GetDouble(args, "centerY") / 1000d, ToolHelpers.GetDouble(args, "centerZ") / 1000d, ToolHelpers.GetDouble(args, "radius") / 1000d]);
            }

            if (result is null)
            {
                throw new InvalidOperationException("Failed to create circle");
            }

            return ValueTask.FromResult<object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["circleId"] = $"circle_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add circle: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddRectangle(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No active model");
            var sketchManager = model.GetType().GetProperty("SketchManager")?.GetValue(model) ?? throw new InvalidOperationException("SketchManager unavailable");
            var result = sketchManager.GetType().GetMethod("CreateCornerRectangle")?.Invoke(sketchManager, [ToolHelpers.GetDouble(args, "x1") / 1000d, ToolHelpers.GetDouble(args, "y1") / 1000d, 0d, ToolHelpers.GetDouble(args, "x2") / 1000d, ToolHelpers.GetDouble(args, "y2") / 1000d, 0d]);
            if (result is null)
            {
                result = sketchManager.GetType().GetMethod("CreateCenterRectangle")?.Invoke(sketchManager, [((ToolHelpers.GetDouble(args, "x1") + ToolHelpers.GetDouble(args, "x2")) / 2d) / 1000d, ((ToolHelpers.GetDouble(args, "y1") + ToolHelpers.GetDouble(args, "y2")) / 2d) / 1000d, 0d, Math.Abs(ToolHelpers.GetDouble(args, "x2") - ToolHelpers.GetDouble(args, "x1")) / 1000d, Math.Abs(ToolHelpers.GetDouble(args, "y2") - ToolHelpers.GetDouble(args, "y1")) / 1000d]);
            }

            return ValueTask.FromResult<object?>(result is null ? ToolHelpers.Failure("Failed to create rectangle") : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = true, ["rectangleId"] = $"rect_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add rectangle: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleExitSketch(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        try
        {
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No active model");
            var sketchManager = model.GetType().GetProperty("SketchManager")?.GetValue(model) ?? throw new InvalidOperationException("SketchManager unavailable");
            sketchManager.GetType().GetMethod("InsertSketch")?.Invoke(sketchManager, [true]);

            if (ToolHelpers.GetBool(args, "rebuild", true))
            {
                model.GetType().GetMethod("EditRebuild3")?.Invoke(model, []);
            }

            return ValueTask.FromResult<object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["message"] = "Exited sketch edit mode",
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to exit sketch: {ex.Message}"));
        }
    }
}
