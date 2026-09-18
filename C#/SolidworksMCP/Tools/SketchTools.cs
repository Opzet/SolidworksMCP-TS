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
        new McpToolDefinition("list_reference_planes", "List reference planes and axes on the active model for declarative selection planning", JsonSchemaBuilder.Object(), HandleListReferencePlanes),
        new McpToolDefinition("create_plane", "Create a reference plane from declarative selection", JsonSchemaBuilder.ObjectWithRequired(["mode", "selection"], ("mode", JsonSchemaBuilder.Enum(["offset", "angle", "midplane", "three_points", "parallel_through_point"], "Reference plane creation mode", "offset")), ("selection", JsonSchemaBuilder.Any("Declarative selection object for plane references")), ("distance_mm", JsonSchemaBuilder.Number("Offset distance for offset mode")), ("angle_deg", JsonSchemaBuilder.Number("Angle for angle mode")), ("flip", JsonSchemaBuilder.Boolean("Flip to the opposite side", false)), ("name", JsonSchemaBuilder.String("Optional feature name"))), HandleCreatePlane),
        new McpToolDefinition("create_axis", "Create a reference axis from declarative selection", JsonSchemaBuilder.ObjectWithRequired(["selection"], ("selection", JsonSchemaBuilder.Any("Declarative selection object for axis references")), ("name", JsonSchemaBuilder.String("Optional feature name"))), HandleCreateAxis),
        new McpToolDefinition("edit_sketch", "Reopen an existing sketch for editing", JsonSchemaBuilder.ObjectWithRequired(["sketch_name"], ("sketch_name", JsonSchemaBuilder.String("Sketch feature name"))), HandleEditSketch),
        new McpToolDefinition("close_sketch", "Close the open sketch without forcing a rebuild", JsonSchemaBuilder.Object(), HandleCloseSketch),
        new McpToolDefinition("list_sketches", "List sketches on the active model with stable handles", JsonSchemaBuilder.Object(), HandleListSketches),
        new McpToolDefinition("list_sketch_segments", "List sketch segments for the active or named sketch with stable handles", JsonSchemaBuilder.Object(("sketch_name", JsonSchemaBuilder.String("Optional sketch feature name"))), HandleListSketchSegments),
        new McpToolDefinition("get_sketch_status", "Report whether the active or named sketch is under-defined, fully-defined, or over-defined", JsonSchemaBuilder.Object(("sketch_name", JsonSchemaBuilder.String("Optional sketch feature name"))), HandleGetSketchStatus),
        new McpToolDefinition("add_line", "Add a line to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x1", "y1", "x2", "y2"], ("x1", JsonSchemaBuilder.Number()), ("y1", JsonSchemaBuilder.Number()), ("z1", JsonSchemaBuilder.Number("Z start", 0)), ("x2", JsonSchemaBuilder.Number()), ("y2", JsonSchemaBuilder.Number()), ("z2", JsonSchemaBuilder.Number("Z end", 0))), HandleAddLine),
        new McpToolDefinition("draw_line", "Add a line to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x1", "y1", "x2", "y2"], ("x1", JsonSchemaBuilder.Number()), ("y1", JsonSchemaBuilder.Number()), ("z1", JsonSchemaBuilder.Number("Z start", 0)), ("x2", JsonSchemaBuilder.Number()), ("y2", JsonSchemaBuilder.Number()), ("z2", JsonSchemaBuilder.Number("Z end", 0))), HandleAddLine),
        new McpToolDefinition("add_circle", "Add a circle to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["centerX", "centerY", "radius"], ("centerX", JsonSchemaBuilder.Number()), ("centerY", JsonSchemaBuilder.Number()), ("centerZ", JsonSchemaBuilder.Number("Center Z", 0)), ("radius", JsonSchemaBuilder.Number())), HandleAddCircle),
        new McpToolDefinition("draw_circle", "Add a circle to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["centerX", "centerY", "radius"], ("centerX", JsonSchemaBuilder.Number()), ("centerY", JsonSchemaBuilder.Number()), ("centerZ", JsonSchemaBuilder.Number("Center Z", 0)), ("radius", JsonSchemaBuilder.Number())), HandleAddCircle),
        new McpToolDefinition("add_rectangle", "Add a rectangle to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x1", "y1", "x2", "y2"], ("x1", JsonSchemaBuilder.Number()), ("y1", JsonSchemaBuilder.Number()), ("x2", JsonSchemaBuilder.Number()), ("y2", JsonSchemaBuilder.Number())), HandleAddRectangle),
        new McpToolDefinition("draw_rectangle", "Add a rectangle to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x1", "y1", "x2", "y2"], ("x1", JsonSchemaBuilder.Number()), ("y1", JsonSchemaBuilder.Number()), ("x2", JsonSchemaBuilder.Number()), ("y2", JsonSchemaBuilder.Number())), HandleAddRectangle),
        new McpToolDefinition("draw_centerline", "Add a construction centerline to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x1", "y1", "x2", "y2"], ("x1", JsonSchemaBuilder.Number()), ("y1", JsonSchemaBuilder.Number()), ("z1", JsonSchemaBuilder.Number("Z start", 0)), ("x2", JsonSchemaBuilder.Number()), ("y2", JsonSchemaBuilder.Number()), ("z2", JsonSchemaBuilder.Number("Z end", 0))), HandleAddCenterLine),
        new McpToolDefinition("draw_point", "Add a sketch point to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x", "y"], ("x", JsonSchemaBuilder.Number()), ("y", JsonSchemaBuilder.Number()), ("z", JsonSchemaBuilder.Number("Z coordinate", 0))), HandleAddPoint),
        new McpToolDefinition("draw_arc", "Add an arc to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["centerX", "centerY", "startX", "startY", "endX", "endY"], ("centerX", JsonSchemaBuilder.Number()), ("centerY", JsonSchemaBuilder.Number()), ("centerZ", JsonSchemaBuilder.Number("Center Z", 0)), ("startX", JsonSchemaBuilder.Number()), ("startY", JsonSchemaBuilder.Number()), ("startZ", JsonSchemaBuilder.Number("Start Z", 0)), ("endX", JsonSchemaBuilder.Number()), ("endY", JsonSchemaBuilder.Number()), ("endZ", JsonSchemaBuilder.Number("End Z", 0)), ("clockwise", JsonSchemaBuilder.Boolean("Clockwise arc direction", false))), HandleAddArc),
        new McpToolDefinition("draw_3point_arc", "Add a 3-point arc to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["x1", "y1", "x2", "y2", "x3", "y3"], ("x1", JsonSchemaBuilder.Number()), ("y1", JsonSchemaBuilder.Number()), ("z1", JsonSchemaBuilder.Number("Point 1 Z", 0)), ("x2", JsonSchemaBuilder.Number()), ("y2", JsonSchemaBuilder.Number()), ("z2", JsonSchemaBuilder.Number("Point 2 Z", 0)), ("x3", JsonSchemaBuilder.Number()), ("y3", JsonSchemaBuilder.Number()), ("z3", JsonSchemaBuilder.Number("Point 3 Z", 0))), HandleAdd3PointArc),
        new McpToolDefinition("draw_ellipse", "Add an ellipse to the current sketch", JsonSchemaBuilder.ObjectWithRequired(["centerX", "centerY", "majorX", "majorY", "minorX", "minorY"], ("centerX", JsonSchemaBuilder.Number()), ("centerY", JsonSchemaBuilder.Number()), ("majorX", JsonSchemaBuilder.Number()), ("majorY", JsonSchemaBuilder.Number()), ("minorX", JsonSchemaBuilder.Number()), ("minorY", JsonSchemaBuilder.Number())), HandleAddEllipse),
        new McpToolDefinition("add_relation", "Add a sketch relation using declarative sketch selection", JsonSchemaBuilder.ObjectWithRequired(["relation", "selection"], ("relation", JsonSchemaBuilder.Enum(["horizontal", "vertical", "tangent", "parallel", "perpendicular", "coincident", "concentric", "symmetric", "midpoint", "intersection", "equal", "fixed", "collinear", "coradial"])), ("selection", JsonSchemaBuilder.Any("Declarative selection object with sketch_segments and/or sketch_points"))), HandleAddRelation),
        new McpToolDefinition("add_dimension", "Add a driving sketch dimension using declarative sketch selection", JsonSchemaBuilder.ObjectWithRequired(["selection"], ("selection", JsonSchemaBuilder.Any("Declarative selection object with sketch_segments and/or sketch_points")), ("value_mm", JsonSchemaBuilder.Number("Linear dimension value in mm")), ("value_deg", JsonSchemaBuilder.Number("Angular dimension value in degrees")), ("kind", JsonSchemaBuilder.Enum(["auto", "horizontal", "vertical", "radius", "diameter"], "Dimension kind", "auto")), ("place_x_mm", JsonSchemaBuilder.Number("Dimension text X in mm", 0)), ("place_y_mm", JsonSchemaBuilder.Number("Dimension text Y in mm", 0)), ("place_z_mm", JsonSchemaBuilder.Number("Dimension text Z in mm", 0))), HandleAddDimension),
        new McpToolDefinition("list_dimensions", "List dimensions on the active model or a specific owning feature/sketch", JsonSchemaBuilder.Object(("owner_name", JsonSchemaBuilder.String("Optional owner feature or sketch name"))), HandleListDimensions),
        new McpToolDefinition("set_construction_geometry", "Toggle construction geometry on sketch segments", JsonSchemaBuilder.ObjectWithRequired(["selection"], ("selection", JsonSchemaBuilder.Any("Selection with sketch_name and sketch_segments")), ("construction", JsonSchemaBuilder.Boolean("True to mark as construction geometry", true))), HandleSetConstructionGeometry),
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

    private static ValueTask<object?> HandleListReferencePlanes(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["planes"] = api.ListReferencePlanes(),
                ["axes"] = api.ListReferenceAxes(),
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to list reference geometry: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCreatePlane(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.CreatePlane(
                ToolHelpers.GetString(args, "mode"),
                ToolHelpers.GetSelection(args),
                args.ContainsKey("distance_mm") ? ToolHelpers.GetDouble(args, "distance_mm") : null,
                args.ContainsKey("angle_deg") ? ToolHelpers.GetDouble(args, "angle_deg") : null,
                ToolHelpers.GetBool(args, "flip"),
                ToolHelpers.GetString(args, "name"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to create plane: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCreateAxis(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.CreateAxis(ToolHelpers.GetSelection(args), ToolHelpers.GetString(args, "name"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to create axis: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleListSketches(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sketches"] = api.ListSketches(),
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to list sketches: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleListSketchSegments(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sketchName"] = ToolHelpers.GetString(args, "sketch_name"),
                ["segments"] = api.ListSketchSegments(ToolHelpers.GetString(args, "sketch_name")),
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to list sketch segments: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleGetSketchStatus(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var sketchName = ToolHelpers.GetString(args, "sketch_name");
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sketchName"] = sketchName,
                ["status"] = api.GetSketchStatus(sketchName),
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to get sketch status: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleEditSketch(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.EditSketch(ToolHelpers.GetString(args, "sketch_name"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to edit sketch: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleCloseSketch(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = cancellationToken;
        try
        {
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.CloseSketch()));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to close sketch: {ex.Message}"));
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

    private static ValueTask<object?> HandleAddCenterLine(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.AddCenterLine(args)));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add centerline: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddPoint(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.AddPoint(args)));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add point: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddArc(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.AddArc(args)));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add arc: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAdd3PointArc(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.Add3PointArc(args)));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add 3-point arc: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddEllipse(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(api.AddEllipse(args)));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add ellipse: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddRelation(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.AddSketchRelation(ToolHelpers.GetString(args, "relation"), ToolHelpers.GetSelection(args));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add sketch relation: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleAddDimension(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var result = api.AddSketchDimension(
                ToolHelpers.GetSelection(args),
                args.ContainsKey("value_mm") ? ToolHelpers.GetDouble(args, "value_mm") : null,
                args.ContainsKey("value_deg") ? ToolHelpers.GetDouble(args, "value_deg") : null,
                ToolHelpers.GetString(args, "kind", "auto"),
                ToolHelpers.GetDouble(args, "place_x_mm"),
                ToolHelpers.GetDouble(args, "place_y_mm"),
                ToolHelpers.GetDouble(args, "place_z_mm"));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to add sketch dimension: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleListDimensions(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var ownerName = ToolHelpers.GetString(args, "owner_name");
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ownerName"] = ownerName,
                ["dimensions"] = api.ListDimensions(ownerName),
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to list dimensions: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleSetConstructionGeometry(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var selection = ToolHelpers.GetSelection(args);
            var result = api.SetConstructionGeometry(selection.SketchName, selection.SketchSegments ?? [], ToolHelpers.GetBool(args, "construction", true));
            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to set construction geometry: {ex.Message}"));
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
