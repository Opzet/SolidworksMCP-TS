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
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"HandleCreateSketch > Failed to create sketch: {ex.Message}"));
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
            var result = api.AddCircle(args);
            return ValueTask.FromResult<object?>(result);
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
            var result = api.AddRectangle(args);
            return ValueTask.FromResult<object?>(result);
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
            var result = api.ExitSketch(ToolHelpers.GetBool(args, "rebuild", true));
            return ValueTask.FromResult<object?>(result);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to exit sketch: {ex.Message}"));
        }
    }
}
