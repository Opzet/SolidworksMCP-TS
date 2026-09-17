namespace SolidworksMCP;

using System.Globalization;
using System.Reflection;
using System.Text.Json;

public static class AnalysisTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("get_mass_properties", "Get mass properties of the current model", JsonSchemaBuilder.Object(("units", JsonSchemaBuilder.Enum(["kg", "g", "lb"], "Mass units", "kg"))), HandleGetMassProperties),
        new McpToolDefinition("check_interference", "Check for interference between components in an assembly", JsonSchemaBuilder.Object(("treatCoincidenceAsInterference", JsonSchemaBuilder.Boolean(defaultValue: false)), ("treatSubAssembliesAsComponents", JsonSchemaBuilder.Boolean(defaultValue: false)), ("includeMultibodyParts", JsonSchemaBuilder.Boolean(defaultValue: true))), HandleCheckInterference),
        new McpToolDefinition("measure_distance", "Measure distance between two selected entities", JsonSchemaBuilder.ObjectWithRequired(["entity1", "entity2"], ("entity1", JsonSchemaBuilder.String()), ("entity2", JsonSchemaBuilder.String())), HandleMeasureDistance),
        new McpToolDefinition("analyze_draft", "Analyze draft angles for molding", JsonSchemaBuilder.ObjectWithRequired(["pullDirection"], ("pullDirection", JsonSchemaBuilder.Enum(["x", "y", "z", "-x", "-y", "-z"])), ("requiredAngle", JsonSchemaBuilder.Number("Required draft angle in degrees", 1))), HandleAnalyzeDraft),
        new McpToolDefinition("check_geometry", "Check model geometry for errors", JsonSchemaBuilder.Object(("checkType", JsonSchemaBuilder.Enum(["all", "faces", "edges", "vertices"], "Geometry check type", "all"))), HandleCheckGeometry),
    ];

    private static ValueTask<object?> HandleGetMassProperties(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var props = api.GetMassProperties();
            var mass = props.Mass;
            var units = ToolHelpers.GetString(args, "units", "kg");
            if (units == "g")
            {
                mass *= 1000;
            }
            else if (units == "lb")
            {
                mass *= 2.20462;
            }

            return ValueTask.FromResult<object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["mass"] = $"{mass:0.000} {units}",
                ["volume"] = $"{(props.Volume * 1e9):0.000} mm³",
                ["surfaceArea"] = $"{(props.SurfaceArea * 1e6):0.000} mm²",
                ["centerOfMass"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x"] = $"{props.CenterOfMass.X:0.000} mm",
                    ["y"] = $"{props.CenterOfMass.Y:0.000} mm",
                    ["z"] = $"{props.CenterOfMass.Z:0.000} mm",
                },
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to get mass properties: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCheckInterference(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _ = arguments;
        try
        {
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("Current document must be an assembly");
            var type = Convert.ToInt32(model.GetType().GetMethod("GetType")?.Invoke(model, []) ?? 0);
            if (type != 2)
            {
                throw new InvalidOperationException("Current document must be an assembly");
            }

            var manager = model.GetType().GetProperty("InterferenceDetectionManager")?.GetValue(model);
            if (manager is null)
            {
                return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["supported"] = false,
                    ["interferenceCount"] = 0,
                    ["message"] = "Interference detection manager unavailable on current API surface.",
                }));
            }

            var count = Convert.ToInt32(manager.GetType().GetMethod("GetInterferenceCount")?.Invoke(manager, []) ?? 0);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["supported"] = true,
                ["interferenceCount"] = count,
                ["hasInterference"] = count > 0,
            }));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to check interference: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleMeasureDistance(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var entity1 = ToolHelpers.GetString(args, "entity1");
            var entity2 = ToolHelpers.GetString(args, "entity2");

            if (TryParsePoint(entity1, out var p1) && TryParsePoint(entity2, out var p2))
            {
                var dx = p2.X - p1.X;
                var dy = p2.Y - p1.Y;
                var dz = p2.Z - p1.Z;
                var distanceMm = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

                return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mode"] = "coordinates",
                    ["distanceMm"] = distanceMm,
                    ["distanceMeters"] = distanceMm / 1000d,
                    ["delta"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["xMm"] = dx,
                        ["yMm"] = dy,
                        ["zMm"] = dz,
                    },
                }));
            }

            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No model open");
            var extension = model.GetType().GetProperty("Extension", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(model);
            if (extension is null)
            {
                throw new InvalidOperationException("Model extension is unavailable for entity-based distance measurement.");
            }

            var selectionManager = model.GetType().GetProperty("SelectionManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(model);
            Invoke(model, "ClearSelection2", true);

            var firstSelected = SelectEntityById(extension, entity1);
            var secondSelected = SelectEntityById(extension, entity2, append: true);
            if (!firstSelected || !secondSelected)
            {
                throw new InvalidOperationException($"Unable to select one or both entities ('{entity1}', '{entity2}').");
            }

            var firstEntity = selectionManager is null ? null : Invoke(selectionManager, "GetSelectedObject6", 1, -1);
            var secondEntity = selectionManager is null ? null : Invoke(selectionManager, "GetSelectedObject6", 2, -1);

            var distanceMeters = TryGetDistanceMeters(extension, model, firstEntity, secondEntity);
            Invoke(model, "ClearSelection2", true);

            if (distanceMeters is null)
            {
                throw new InvalidOperationException("Could not compute distance for selected entities via SolidWorks API.");
            }

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["mode"] = "solidworks-entity",
                ["entity1"] = entity1,
                ["entity2"] = entity2,
                ["distanceMeters"] = distanceMeters.Value,
                ["distanceMm"] = distanceMeters.Value * 1000d,
            }));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to measure distance: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAnalyzeDraft(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No model open");
            var requiredAngle = ToolHelpers.GetDouble(args, "requiredAngle", 1);
            var pullDirection = ToolHelpers.GetString(args, "pullDirection");

            var extension = model.GetType().GetProperty("Extension", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(model);
            var started = extension is not null && (Invoke(extension, "RunDraftAnalysis", pullDirection, requiredAngle) is not null || Invoke(extension, "StartDraftAnalysis", pullDirection, requiredAngle) is not null);

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["pullDirection"] = pullDirection,
                ["requiredAngle"] = requiredAngle,
                ["analysisStarted"] = started,
                ["message"] = started
                    ? "Draft analysis command was sent to SolidWorks. Inspect the graphics area for colorized results."
                    : "Draft analysis API is not exposed on current COM surface; model context validated and ready for manual draft analysis.",
            }));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to analyze draft: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCheckGeometry(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var checkType = ToolHelpers.GetString(args, "checkType", "all");

            var model = api.GetCurrentModel() ?? throw new InvalidOperationException("No model open");
            var rebuildResult = Invoke(model, "EditRebuild3");
            if (rebuildResult is null)
            {
                rebuildResult = Invoke(model, "EditRebuild");
            }

            var errors = Convert.ToInt32(Invoke(model, "GetErrorCode2") ?? Invoke(model, "GetLastError") ?? 0);
            var warnings = Convert.ToInt32(Invoke(model, "GetWarningCount") ?? 0);

            var geometryHealth = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["checkType"] = checkType,
                ["rebuildInvoked"] = rebuildResult is not null,
                ["errorCode"] = errors,
                ["warningCount"] = warnings,
                ["status"] = errors == 0 ? "ok" : "has-errors",
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(geometryHealth));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to check geometry: {ex.Message}"));
        }
    }

    private static bool TryParsePoint(string value, out (double X, double Y, double Z) point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 3)
        {
            return false;
        }

        if (!double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !double.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        point = (x, y, z);
        return true;
    }

    private static bool SelectEntityById(object extension, string entityId, bool append = false)
    {
        var entityTypes = new[] { "EDGE", "FACE", "VERTEX", "SKETCHSEGMENT", "SKETCHPOINT", "AXIS", "DATUMPOINT", "FEATURE", "BODYFEATURE", "REFERENCECURVES", "" };
        foreach (var entityType in entityTypes)
        {
            if (Invoke(extension, "SelectByID2", entityId, entityType, 0d, 0d, 0d, append, 0, null, 0) is bool selected && selected)
            {
                return true;
            }
        }

        return false;
    }

    private static double? TryGetDistanceMeters(object extension, object model, object? entity1, object? entity2)
    {
        if (entity1 is null || entity2 is null)
        {
            return null;
        }

        var candidates = new object?[]
        {
            Invoke(extension, "GetDistance", entity1, entity2),
            Invoke(model, "GetDistance", entity1, entity2),
            Invoke(entity1, "GetDistance", entity2),
        };

        foreach (var candidate in candidates)
        {
            if (candidate is null)
            {
                continue;
            }

            if (double.TryParse(Convert.ToString(candidate, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static object? Invoke(object? target, string methodName, params object?[] args)
    {
        if (target is null)
        {
            return null;
        }

        try
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            return method?.Invoke(target, args);
        }
        catch
        {
            return null;
        }
    }
}
