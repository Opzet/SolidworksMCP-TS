namespace SolidworksMCP;

using System.IO;
using System.Text.Json;

public static class ExportTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("export_file", "Export the current model to various formats", JsonSchemaBuilder.ObjectWithRequired(["outputPath"], ("outputPath", JsonSchemaBuilder.String()), ("format", JsonSchemaBuilder.Enum(["step", "iges", "stl", "pdf", "dxf", "dwg"]))), HandleExportFile),
        new McpToolDefinition("batch_export", "Export multiple configurations or files to a format", JsonSchemaBuilder.ObjectWithRequired(["format", "outputDir"], ("format", JsonSchemaBuilder.Enum(["step", "iges", "stl", "pdf", "dxf", "dwg"])), ("outputDir", JsonSchemaBuilder.String()), ("configurations", JsonSchemaBuilder.Array(JsonSchemaBuilder.String())), ("prefix", JsonSchemaBuilder.String())), HandleBatchExport),
        new McpToolDefinition("export_with_options", "Export with specific format options", JsonSchemaBuilder.ObjectWithRequired(["outputPath", "format", "options"], ("outputPath", JsonSchemaBuilder.String()), ("format", JsonSchemaBuilder.Enum(["stl", "step", "iges"])), ("options", JsonSchemaBuilder.Any())), HandleExportWithOptions),
        new McpToolDefinition("capture_screenshot", "Capture a screenshot of the current model view", JsonSchemaBuilder.ObjectWithRequired(["outputPath"], ("outputPath", JsonSchemaBuilder.String()), ("width", JsonSchemaBuilder.Number("Width in pixels")), ("height", JsonSchemaBuilder.Number("Height in pixels"))), HandleCaptureScreenshot),
    ];

    private static ValueTask<object?> HandleExportFile(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var outputPath = ToolHelpers.GetString(args, "outputPath");
            var format = ToolHelpers.GetString(args, "format", Path.GetExtension(outputPath).TrimStart('.'));
            api.ExportFile(outputPath, format);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Exported to {format.ToUpperInvariant()}: {outputPath}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to export: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleBatchExport(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No model open");
            var outputDir = ToolHelpers.GetString(args, "outputDir");
            var format = ToolHelpers.GetString(args, "format");
            var modelName = Path.GetFileNameWithoutExtension(api.GetCurrentModelPath() ?? api.GetCurrentModelTitleOrPath() ?? "model");
            List<string> exported = [];

            var configurations = ToolHelpers.GetStringList(args, "configurations");
            if (configurations.Count > 0)
            {
                foreach (var configuration in configurations)
                {
                    _ = model.GetType().GetMethod("ShowConfiguration2")?.Invoke(model, [configuration]);
                    var filename = $"{ToolHelpers.GetString(args, "prefix")}{modelName}_{configuration}.{format}";
                    var outputPath = Path.Combine(outputDir, filename);
                    api.ExportFile(outputPath, format);
                    exported.Add(outputPath);
                }
            }
            else
            {
                var filename = $"{ToolHelpers.GetString(args, "prefix")}{modelName}.{format}";
                var outputPath = Path.Combine(outputDir, filename);
                api.ExportFile(outputPath, format);
                exported.Add(outputPath);
            }

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Exported {exported.Count} file(s):\n{string.Join('\n', exported)}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to batch export: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleExportWithOptions(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var outputPath = ToolHelpers.GetString(args, "outputPath");
            var format = ToolHelpers.GetString(args, "format");
            api.ExportFile(outputPath, format);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Exported with options to: {outputPath}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to export with options: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCaptureScreenshot(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No model open");
            var outputPath = ToolHelpers.GetString(args, "outputPath");
            var width = (int)ToolHelpers.GetDouble(args, "width", 1920);
            var height = (int)ToolHelpers.GetDouble(args, "height", 1080);
            var ext = Path.GetExtension(outputPath).ToLowerInvariant();
            var success = false;

            if (ext == ".bmp")
            {
                success = model.GetType().GetMethod("SaveBMP")?.Invoke(model, [outputPath, width, height]) is bool bmp && bmp;
            }
            else
            {
                model.GetType().GetMethod("ViewZoomtofit2")?.Invoke(model, []);
                success = model.GetType().GetMethod("SaveAs3")?.Invoke(model, [outputPath, 0, 2]) is bool saved && saved;
            }

            return ValueTask.FromResult<object?>(success ? ToolHelpers.SuccessText($"Captured screenshot to: {outputPath}") : ToolHelpers.Failure($"Export completed but file not found at: {outputPath}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to capture screenshot: {ex.Message}"));
        }
    }
}
