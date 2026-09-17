namespace SolidworksMCP;

using System.Text;
using System.Text.Json;

public static class VbaTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("generate_vba_script", "Generate a VBA script from a template with parameters", JsonSchemaBuilder.ObjectWithRequired(["template", "parameters"], ("template", JsonSchemaBuilder.String()), ("parameters", JsonSchemaBuilder.Any()), ("outputPath", JsonSchemaBuilder.String())), HandleGenerateVbaScript),
        new McpToolDefinition("create_feature_vba", "Generate VBA code to create a specific feature", JsonSchemaBuilder.ObjectWithRequired(["featureType", "parameters"], ("featureType", JsonSchemaBuilder.Enum(["extrude", "revolve", "sweep", "loft", "hole", "fillet", "chamfer"])), ("parameters", JsonSchemaBuilder.Any())), HandleCreateFeatureVba),
        new McpToolDefinition("create_batch_vba", "Generate VBA for batch processing multiple files", JsonSchemaBuilder.ObjectWithRequired(["operation", "filePattern"], ("operation", JsonSchemaBuilder.Enum(["export", "update_property", "rebuild", "print"])), ("filePattern", JsonSchemaBuilder.String()), ("outputFormat", JsonSchemaBuilder.String()), ("propertyName", JsonSchemaBuilder.String()), ("propertyValue", JsonSchemaBuilder.String())), HandleCreateBatchVba),
    ];

    private static ValueTask<object?> HandleGenerateVbaScript(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = api;
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var templateName = ToolHelpers.GetString(args, "template");
            var parameters = ToolHelpers.GetDictionary(args, "parameters");
            var outputPath = ToolHelpers.GetString(args, "outputPath");
            var template = LoadTemplate(templateName);
            var vbaCode = ApplyTemplate(template, parameters);

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                File.WriteAllText(outputPath, vbaCode, Encoding.UTF8);
                return ValueTask.FromResult<object?>(ToolHelpers.SuccessText($"VBA script generated and saved to: {outputPath}"));
            }

            return ValueTask.FromResult<object?>(vbaCode);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to generate VBA script: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCreateFeatureVba(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = api;
        _ = cancellationToken;

        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var featureType = ToolHelpers.GetString(args, "featureType").ToLowerInvariant();
            var parameters = ToolHelpers.GetDictionary(args, "parameters");

            var depth = ToolHelpers.GetDouble(parameters, "depth", 10);
            var radius = ToolHelpers.GetDouble(parameters, "radius", 5);
            var draft = ToolHelpers.GetDouble(parameters, "draft", 0);
            var angle = ToolHelpers.GetDouble(parameters, "angle", 360);

            var code = featureType switch
            {
                "extrude" => BuildExtrudeVba(depth, draft),
                "revolve" => BuildRevolveVba(angle),
                "sweep" => BuildSweepVba(),
                "loft" => BuildLoftVba(),
                "hole" => BuildHoleVba(depth, radius),
                "fillet" => BuildFilletVba(radius),
                "chamfer" => BuildChamferVba(depth, angle),
                _ => throw new InvalidOperationException($"Unsupported featureType '{featureType}'."),
            };

            return ValueTask.FromResult<object?>(code);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to generate feature VBA: {ex.Message}"));
        }
    }

    private static string BuildExtrudeVba(double depth, double draft)
    {
        return $$"""
Sub CreateExtrusion()
    Dim swApp As SldWorks.SldWorks
    Dim swModel As SldWorks.ModelDoc2
    Dim swFeature As SldWorks.Feature

    Set swApp = Application.SldWorks
    Set swModel = swApp.ActiveDoc

    If Not swModel Is Nothing Then
        Set swFeature = swModel.FeatureManager.FeatureExtrusion3(True, False, False, 0, 0, {{depth}} / 1000, {{draft}} / 1000, False, False, False, False, 0, 0, False, False, False, False, False, True, True, 0, 0, False)
    End If
End Sub
""";
    }

    private static string BuildHoleVba(double depth, double radius)
    {
        return $$"""
Sub CreateHole()
    Dim swApp As SldWorks.SldWorks
    Dim swModel As SldWorks.ModelDoc2
    Dim swFeature As SldWorks.Feature

    Set swApp = Application.SldWorks
    Set swModel = swApp.ActiveDoc

    If Not swModel Is Nothing Then
        Set swFeature = swModel.FeatureManager.HoleWizard5(0, 1, 1, "C:\\ProgramData\\SolidWorks\\SOLIDWORKS 2024\\lang\\english\\swstandards\\ansi inch\\dowel pins.mdb", "Dowel Pin", "DIN", "All", {{radius}} / 1000, 1, {{depth}} / 1000, 0, 1, 0, 0, 0, 0, 0, "", False, True, True, True, True, False)
    End If
End Sub
""";
    }

    private static string BuildRevolveVba(double angle)
    {
        return $$"""
Sub CreateRevolve()
    Dim swApp As SldWorks.SldWorks
    Dim swModel As SldWorks.ModelDoc2
    Dim swFeature As SldWorks.Feature

    Set swApp = Application.SldWorks
    Set swModel = swApp.ActiveDoc

    If Not swModel Is Nothing Then
        Set swFeature = swModel.FeatureManager.FeatureRevolve2(True, True, False, False, False, False, 0, 0, {{angle}} * 0.0174532925199433, 0, False, False, 0, 0, 0, 0, 0, True, True, True)
    End If
End Sub
""";
    }

    private static string BuildSweepVba()
    {
        return """
Sub CreateSweep()
    Dim swApp As SldWorks.SldWorks
    Dim swModel As SldWorks.ModelDoc2
    Dim swFeature As SldWorks.Feature

    Set swApp = Application.SldWorks
    Set swModel = swApp.ActiveDoc

    If Not swModel Is Nothing Then
        Set swFeature = swModel.FeatureManager.InsertProtrusionSwept4(False, False, 0, False, False, 0, 0, 0, 0, 0, False, False, False, 0, 0, False, False, False, False)
    End If
End Sub
""";
    }

    private static string BuildLoftVba()
    {
        return """
Sub CreateLoft()
    Dim swApp As SldWorks.SldWorks
    Dim swModel As SldWorks.ModelDoc2
    Dim swFeature As SldWorks.Feature

    Set swApp = Application.SldWorks
    Set swModel = swApp.ActiveDoc

    If Not swModel Is Nothing Then
        Set swFeature = swModel.FeatureManager.InsertProtrusionBlend2(False, False, False, 1, 0, 0, 0, 0, 0, 0, False, False, 0, 0, 0)
    End If
End Sub
""";
    }

    private static string BuildFilletVba(double radius)
    {
        return $$"""
Sub CreateFillet()
    Dim swApp As SldWorks.SldWorks
    Dim swModel As SldWorks.ModelDoc2

    Set swApp = Application.SldWorks
    Set swModel = swApp.ActiveDoc

    If Not swModel Is Nothing Then
        swModel.FeatureManager.InsertFeatureFillet 0, {{radius}} / 1000, 0, 0, 0
    End If
End Sub
""";
    }

    private static string BuildChamferVba(double depth, double angle)
    {
        return $$"""
Sub CreateChamfer()
    Dim swApp As SldWorks.SldWorks
    Dim swModel As SldWorks.ModelDoc2

    Set swApp = Application.SldWorks
    Set swModel = swApp.ActiveDoc

    If Not swModel Is Nothing Then
        swModel.FeatureManager.InsertFeatureChamfer 4, {{depth}} / 1000, {{angle}} * 0.0174532925199433, 0, 0, 0, 0
    End If
End Sub
""";
    }

    private static ValueTask<object?> HandleCreateBatchVba(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = api;
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var operation = ToolHelpers.GetString(args, "operation");
            var filePattern = ToolHelpers.GetString(args, "filePattern");
            var outputFormat = ToolHelpers.GetString(args, "outputFormat");
            var propertyName = ToolHelpers.GetString(args, "propertyName");
            var propertyValue = ToolHelpers.GetString(args, "propertyValue");
            var template = $$"""
Sub BatchProcess()
    Dim operation As String
    operation = "{{operation}}"
    Dim filePattern As String
    filePattern = "{{filePattern}}"
    Dim outputFormat As String
    outputFormat = "{{outputFormat}}"
    Dim propertyName As String
    propertyName = "{{propertyName}}"
    Dim propertyValue As String
    propertyValue = "{{propertyValue}}"
End Sub
""";
            var code = ApplyTemplate(template, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["operation"] = operation,
                ["filePattern"] = filePattern,
                ["outputFormat"] = outputFormat,
                ["propertyName"] = propertyName,
                ["propertyValue"] = propertyValue,
            });
            return ValueTask.FromResult<object?>(code);
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to generate batch VBA: {ex.Message}"));
        }
    }

    private static string LoadTemplate(string templateName)
    {
        var templateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["batch_export"] = "batch_process",
            ["create_drawing"] = "create_drawing",
            ["modify_dimensions"] = "modify_dimensions",
        };

        var actualName = templateMap.TryGetValue(templateName, out var mapped) ? mapped : templateName;
        var templatePath = Path.Combine(AppContext.BaseDirectory, "examples", "vba-templates", $"{actualName}.vba");
        if (File.Exists(templatePath))
        {
            return File.ReadAllText(templatePath);
        }

        return templateName switch
        {
            "batch_export" => "Sub BatchExport()\nEnd Sub",
            "create_drawing" => "Sub CreateDrawing()\nEnd Sub",
            "modify_dimensions" => "Sub ModifyDimensions()\nEnd Sub",
            _ => "Sub Main()\nEnd Sub",
        };
    }

    private static string ApplyTemplate(string template, Dictionary<string, object?> parameters)
    {
        var result = template;
        foreach (var pair in parameters)
        {
            result = result.Replace($"{{{{{pair.Key}}}}}", Convert.ToString(pair.Value) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
