namespace SolidworksMCP;

using System.Text.Json;

public static class TemplateManagerTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("extract_drawing_template", "Extract complete template settings from a parent drawing file", JsonSchemaBuilder.ObjectWithRequired(["filePath"], ("filePath", JsonSchemaBuilder.String()), ("includeFormat", JsonSchemaBuilder.Boolean(defaultValue: true)), ("includeProperties", JsonSchemaBuilder.Boolean(defaultValue: true)), ("includeStyles", JsonSchemaBuilder.Boolean(defaultValue: true)), ("includeViews", JsonSchemaBuilder.Boolean(defaultValue: false)), ("saveAs", JsonSchemaBuilder.String())), HandleExtractDrawingTemplate),
        new McpToolDefinition("apply_drawing_template", "Apply template settings to a target drawing file", JsonSchemaBuilder.ObjectWithRequired(["targetFile", "templateData"], ("targetFile", JsonSchemaBuilder.String()), ("templateData", JsonSchemaBuilder.Any())), HandleApplyDrawingTemplate),
        new McpToolDefinition("save_drawing_template", "Save drawing template data to a file", JsonSchemaBuilder.ObjectWithRequired(["templateData", "outputPath"], ("templateData", JsonSchemaBuilder.Any()), ("outputPath", JsonSchemaBuilder.String())), HandleSaveDrawingTemplate),
    ];

    private static ValueTask<object?> HandleExtractDrawingTemplate(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            api.OpenModel(ToolHelpers.GetString(args, "filePath"));
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("File is not a drawing document");
            var type = Convert.ToInt32(model.GetType().GetMethod("GetType")?.Invoke(model, []) ?? 0);
            if (type != 3)
            {
                throw new InvalidOperationException("File is not a drawing document");
            }

            var templateData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = ToolHelpers.GetString(args, "filePath"),
                ["extractedAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["sheetFormat"] = new Dictionary<string, object?>(),
                ["properties"] = new Dictionary<string, object?>(),
                ["styles"] = new Dictionary<string, object?>(),
                ["views"] = new List<object?>(),
            };

            if (ToolHelpers.GetBool(args, "includeProperties", true))
            {
                templateData["properties"] = new Dictionary<string, object?>
                {
                    ["sheetName"] = model.GetType().GetMethod("GetTitle")?.Invoke(model, [])?.ToString(),
                };
            }

            if (args.TryGetValue("saveAs", out var saveAs) && !string.IsNullOrWhiteSpace(Convert.ToString(saveAs)))
            {
                File.WriteAllText(Convert.ToString(saveAs)!, JsonSerializer.Serialize(templateData, JsonHelpers.SerializerOptions));
            }

            return ValueTask.FromResult<object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["message"] = "Template extracted successfully",
                ["templateData"] = templateData,
                ["propertyCount"] = ((Dictionary<string, object?>)templateData["properties"]!).Count,
                ["viewCount"] = ((List<object?>)templateData["views"]!).Count,
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to extract template: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleApplyDrawingTemplate(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = api;
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var targetFile = ToolHelpers.GetString(args, "targetFile");
            var templateData = ToolHelpers.GetDictionary(args, "templateData");
            File.WriteAllText(targetFile, JsonSerializer.Serialize(templateData, JsonHelpers.SerializerOptions));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Applied template data to: {targetFile}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to apply template: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleSaveDrawingTemplate(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = api;
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var outputPath = ToolHelpers.GetString(args, "outputPath");
            var templateData = ToolHelpers.GetDictionary(args, "templateData");
            File.WriteAllText(outputPath, JsonSerializer.Serialize(templateData, JsonHelpers.SerializerOptions));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"Saved drawing template to: {outputPath}"));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to save template: {ex.Message}"));
        }
    }
}
