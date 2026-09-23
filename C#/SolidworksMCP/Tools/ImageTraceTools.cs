namespace SolidworksMCP;

using System.Drawing;
using System.Text.Json;

public static class ImageTraceTools
{
    /// <summary>
    /// Returns MCP tool definitions for deterministic image tracing and picture-to-sketch workflows.
    /// </summary>
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition(
            "get_vectorization_capabilities",
            "Report vectorization backends/modes, quality-gate support, and current availability.",
            JsonSchemaBuilder.Object(),
            HandleGetVectorizationCapabilities),
        new McpToolDefinition(
            "trace_image_line_art",
            "Extract deterministic line-art segments from an image without using SOLIDWORKS interactive AutoTrace UI.",
            JsonSchemaBuilder.ObjectWithRequired(
                ["input_path"],
                ("input_path", JsonSchemaBuilder.String("Path to the source image file")),
                ("backend", JsonSchemaBuilder.Enum(["deterministic_cpu", "deep_line_art", "gpu_segmentation"], "Preferred backend; unsupported backends fall back to deterministic CPU", "deterministic_cpu")),
                ("mode", JsonSchemaBuilder.Enum(["stroke_centerlines", "stroke_edges", "all_visible_edges", "outer_silhouette", "silhouette_with_holes"], "Trace mode", "all_visible_edges")),
                ("threshold", JsonSchemaBuilder.Integer("Binary threshold 0-255; defaults to auto (Otsu) when omitted")),
                ("invert", JsonSchemaBuilder.Boolean("Invert foreground/background before segmentation", false)),
                ("min_segment_length_px", JsonSchemaBuilder.Number("Minimum segment length in pixels", 2)),
                ("max_segments", JsonSchemaBuilder.Integer("Maximum number of segments to return", 2000)),
                ("quality_gate", JsonSchemaBuilder.Boolean("Compute reverse-raster quality metrics", true))),
            HandleTraceImageLineArt),
        new McpToolDefinition(
            "picture_to_sketch",
            "Create sketch lines from deterministic image line-art segmentation (API-safe replacement for AutoTrace/Picture-to-Sketch).",
            JsonSchemaBuilder.ObjectWithRequired(
                ["input_path"],
                ("input_path", JsonSchemaBuilder.String("Path to the source image file")),
                ("sketch_name", JsonSchemaBuilder.String("Optional existing sketch to edit instead of creating a new sketch")),
                ("plane", JsonSchemaBuilder.Enum(["Front", "Top", "Right", "Custom"], "Sketch plane", "Front")),
                ("backend", JsonSchemaBuilder.Enum(["deterministic_cpu", "deep_line_art", "gpu_segmentation"], "Preferred backend; unsupported backends fall back to deterministic CPU", "deterministic_cpu")),
                ("mode", JsonSchemaBuilder.Enum(["stroke_centerlines", "stroke_edges", "all_visible_edges", "outer_silhouette", "silhouette_with_holes"], "Trace mode", "all_visible_edges")),
                ("threshold", JsonSchemaBuilder.Integer("Binary threshold 0-255; defaults to auto (Otsu) when omitted")),
                ("invert", JsonSchemaBuilder.Boolean("Invert foreground/background before segmentation", false)),
                ("min_segment_length_px", JsonSchemaBuilder.Number("Minimum segment length in pixels", 2)),
                ("max_segments", JsonSchemaBuilder.Integer("Maximum number of segments to draw", 1200)),
                ("width_mm", JsonSchemaBuilder.Number("Target traced width in millimeters", 100)),
                ("height_mm", JsonSchemaBuilder.Number("Optional target traced height in millimeters")),
                ("origin_x_mm", JsonSchemaBuilder.Number("Sketch X origin in millimeters", 0)),
                ("origin_y_mm", JsonSchemaBuilder.Number("Sketch Y origin in millimeters", 0)),
                ("close_sketch", JsonSchemaBuilder.Boolean("Close sketch after drawing", true)),
                ("rebuild", JsonSchemaBuilder.Boolean("Rebuild model when closing sketch", true)),
                ("quality_gate", JsonSchemaBuilder.Boolean("Compute reverse-raster quality metrics", true))),
            HandlePictureToSketch),
        new McpToolDefinition(
            "image_to_sketch",
            "Alias of picture_to_sketch with deep vectorization-compatible mode/backend fields.",
            JsonSchemaBuilder.ObjectWithRequired(
                ["input_path"],
                ("input_path", JsonSchemaBuilder.String("Path to the source image file")),
                ("sketch_name", JsonSchemaBuilder.String("Optional existing sketch to edit instead of creating a new sketch")),
                ("plane", JsonSchemaBuilder.Enum(["Front", "Top", "Right", "Custom"], "Sketch plane", "Front")),
                ("backend", JsonSchemaBuilder.Enum(["deterministic_cpu", "deep_line_art", "gpu_segmentation"], "Preferred backend; unsupported backends fall back to deterministic CPU", "deterministic_cpu")),
                ("mode", JsonSchemaBuilder.Enum(["stroke_centerlines", "stroke_edges", "all_visible_edges", "outer_silhouette", "silhouette_with_holes"], "Trace mode", "all_visible_edges")),
                ("threshold", JsonSchemaBuilder.Integer("Binary threshold 0-255; defaults to auto (Otsu) when omitted")),
                ("invert", JsonSchemaBuilder.Boolean("Invert foreground/background before segmentation", false)),
                ("min_segment_length_px", JsonSchemaBuilder.Number("Minimum segment length in pixels", 2)),
                ("max_segments", JsonSchemaBuilder.Integer("Maximum number of segments to draw", 1200)),
                ("width_mm", JsonSchemaBuilder.Number("Target traced width in millimeters", 100)),
                ("height_mm", JsonSchemaBuilder.Number("Optional target traced height in millimeters")),
                ("origin_x_mm", JsonSchemaBuilder.Number("Sketch X origin in millimeters", 0)),
                ("origin_y_mm", JsonSchemaBuilder.Number("Sketch Y origin in millimeters", 0)),
                ("close_sketch", JsonSchemaBuilder.Boolean("Close sketch after drawing", true)),
                ("rebuild", JsonSchemaBuilder.Boolean("Rebuild model when closing sketch", true)),
                ("quality_gate", JsonSchemaBuilder.Boolean("Compute reverse-raster quality metrics", true))),
            HandlePictureToSketch),
    ];

    private static ValueTask<object?> HandleGetVectorizationCapabilities(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = api;
        _ = cancellationToken;

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["deepVectorizationAvailable"] = false,
            ["activeBackend"] = "deterministic_cpu",
            ["availableBackends"] = new[] { "deterministic_cpu" },
            ["declaredBackends"] = new[] { "deterministic_cpu", "deep_line_art", "gpu_segmentation" },
            ["supportedModes"] = new[] { "stroke_centerlines", "stroke_edges", "all_visible_edges", "outer_silhouette", "silhouette_with_holes" },
            ["qualityGates"] = new[] { "reverse_raster_iou", "edge_pixel_occupancy", "segment_density" },
            ["notes"] = "GPU/deep backends are declared for compatibility but currently fallback to deterministic CPU in this C# implementation.",
        };

        return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(payload));
    }

    private static ValueTask<object?> HandleTraceImageLineArt(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = api;

        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var options = BuildOptions(args, 2000);
            var sourcePath = GetValidatedInputPath(args);

            using var bitmap = new Bitmap(sourcePath);
            var trace = TraceLineArt(bitmap, options, cancellationToken);

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["inputPath"] = sourcePath,
                ["backendRequested"] = options.Backend,
                ["backendUsed"] = options.ResolvedBackend,
                ["mode"] = options.Mode,
                ["widthPx"] = trace.Width,
                ["heightPx"] = trace.Height,
                ["threshold"] = trace.Threshold,
                ["invert"] = options.Invert,
                ["segmentCount"] = trace.Segments.Count,
                ["segments"] = trace.Segments.Select(segment => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x1_px"] = segment.X1,
                    ["y1_px"] = segment.Y1,
                    ["x2_px"] = segment.X2,
                    ["y2_px"] = segment.Y2,
                    ["length_px"] = segment.LengthPx,
                }).Cast<object?>().ToList(),
                ["quality"] = options.QualityGate ? BuildQualityMetrics(trace) : null,
                ["message"] = $"Extracted {trace.Segments.Count} deterministic line-art segment(s).",
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(payload));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to trace image line-art: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandlePictureToSketch(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var options = BuildOptions(args, 1200);
            var sourcePath = GetValidatedInputPath(args);
            var plane = ToolHelpers.GetString(args, "plane", "Front");
            var targetWidthMm = Math.Max(1d, ToolHelpers.GetDouble(args, "width_mm", 100d));
            var targetHeightMm = ToolHelpers.GetDouble(args, "height_mm", -1d);
            var originXmm = ToolHelpers.GetDouble(args, "origin_x_mm", 0d);
            var originYmm = ToolHelpers.GetDouble(args, "origin_y_mm", 0d);
            var closeSketch = ToolHelpers.GetBool(args, "close_sketch", true);
            var rebuild = ToolHelpers.GetBool(args, "rebuild", true);

            using var bitmap = new Bitmap(sourcePath);
            var trace = TraceLineArt(bitmap, options, cancellationToken);
            if (trace.Segments.Count == 0)
            {
                return ValueTask.FromResult<object?>(ToolHelpers.Failure("No line-art segments were detected from the source image."));
            }

            var sketchName = ToolHelpers.GetString(args, "sketch_name").Trim();
            object sketchActivationResult;
            if (!string.IsNullOrWhiteSpace(sketchName))
            {
                sketchActivationResult = api.EditSketch(sketchName);
                if (!IsSuccessResult(sketchActivationResult))
                {
                    return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Could not edit sketch '{sketchName}'."));
                }
            }
            else
            {
                sketchActivationResult = api.CreateSketch(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["plane"] = plane,
                });

                if (!IsSuccessResult(sketchActivationResult))
                {
                    return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Could not create sketch on plane '{plane}'."));
                }
            }

            var widthDenominator = Math.Max(1d, trace.Width - 1d);
            var heightDenominator = Math.Max(1d, trace.Height - 1d);
            var scaleX = targetWidthMm / widthDenominator;
            var scaleY = targetHeightMm > 0d ? targetHeightMm / heightDenominator : scaleX;

            var attempted = 0;
            var added = 0;

            foreach (var segment in trace.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                attempted++;
                var addLineResult = api.AddLine(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x1"] = originXmm + (segment.X1 * scaleX),
                    ["y1"] = originYmm + ((trace.Height - 1 - segment.Y1) * scaleY),
                    ["z1"] = 0d,
                    ["x2"] = originXmm + (segment.X2 * scaleX),
                    ["y2"] = originYmm + ((trace.Height - 1 - segment.Y2) * scaleY),
                    ["z2"] = 0d,
                });

                if (IsSuccessResult(addLineResult))
                {
                    added++;
                }
            }

            object? closeResult = null;
            if (closeSketch)
            {
                closeResult = api.ExitSketch(rebuild);
            }

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = added > 0,
                ["inputPath"] = sourcePath,
                ["plane"] = plane,
                ["sketchName"] = string.IsNullOrWhiteSpace(sketchName) ? null : sketchName,
                ["sketchActivation"] = sketchActivationResult,
                ["backendRequested"] = options.Backend,
                ["backendUsed"] = options.ResolvedBackend,
                ["mode"] = options.Mode,
                ["threshold"] = trace.Threshold,
                ["segmentCount"] = trace.Segments.Count,
                ["attemptedLines"] = attempted,
                ["addedLines"] = added,
                ["closeSketch"] = closeSketch,
                ["closeSketchResult"] = closeResult,
                ["quality"] = options.QualityGate ? BuildQualityMetrics(trace) : null,
                ["message"] = added > 0
                    ? $"Inserted {added} line(s) from deterministic picture-to-sketch tracing."
                    : "No sketch lines were added from traced segments.",
            };

            return ValueTask.FromResult<object?>(ToolHelpers.SuccessObject(payload));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to create sketch from picture: {ex.Message}"));
        }
    }

    private static TraceOptions BuildOptions(IDictionary<string, object?> args, int defaultMaxSegments)
    {
        var threshold = args.ContainsKey("threshold") ? (int?)Math.Clamp((int)Math.Round(ToolHelpers.GetDouble(args, "threshold", 0)), 0, 255) : null;
        var minSegmentLengthPx = Math.Max(1d, ToolHelpers.GetDouble(args, "min_segment_length_px", 2d));
        var maxSegments = Math.Max(1, (int)Math.Round(ToolHelpers.GetDouble(args, "max_segments", defaultMaxSegments)));
        var mode = ToolHelpers.GetString(args, "mode", "all_visible_edges").Trim().ToLowerInvariant();
        var backend = ToolHelpers.GetString(args, "backend", "deterministic_cpu").Trim().ToLowerInvariant();
        var resolvedBackend = backend is "gpu_segmentation" or "deep_line_art" ? "deterministic_cpu" : backend;
        var qualityGate = ToolHelpers.GetBool(args, "quality_gate", true);

        return new TraceOptions(threshold, ToolHelpers.GetBool(args, "invert", false), minSegmentLengthPx, maxSegments, mode, backend, resolvedBackend, qualityGate);
    }

    private static string GetValidatedInputPath(IDictionary<string, object?> args)
    {
        var inputPath = ToolHelpers.GetString(args, "input_path").Trim();
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("input_path is required.");
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Image file was not found: {inputPath}", inputPath);
        }

        return inputPath;
    }

    private static TraceResult TraceLineArt(Bitmap bitmap, TraceOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var grayscale = BuildGrayscale(bitmap, cancellationToken);
        var threshold = options.Threshold ?? ComputeOtsuThreshold(grayscale);
        var mask = BuildBinaryMask(grayscale, threshold, options.Invert, cancellationToken);
        var edges = BuildEdgeMask(mask, options.Mode, cancellationToken);
        var edgePixelCount = CountTrue(edges);
        var segments = BuildSegments(edges, options.MinSegmentLengthPx, options.MaxSegments, cancellationToken);

        return new TraceResult(bitmap.Width, bitmap.Height, threshold, edgePixelCount, segments);
    }

    private static byte[,] BuildGrayscale(Bitmap bitmap, CancellationToken cancellationToken)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var grayscale = new byte[height, width];

        for (var y = 0; y < height; y++)
        {
            if ((y & 31) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            for (var x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                grayscale[y, x] = (byte)Math.Clamp((int)Math.Round((0.299d * pixel.R) + (0.587d * pixel.G) + (0.114d * pixel.B)), 0, 255);
            }
        }

        return grayscale;
    }

    private static int ComputeOtsuThreshold(byte[,] grayscale)
    {
        var histogram = new int[256];
        var height = grayscale.GetLength(0);
        var width = grayscale.GetLength(1);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                histogram[grayscale[y, x]]++;
            }
        }

        var total = width * height;
        long sum = 0;
        for (var value = 0; value < histogram.Length; value++)
        {
            sum += value * histogram[value];
        }

        long sumBackground = 0;
        var weightBackground = 0;
        var maxVariance = double.MinValue;
        var threshold = 127;

        for (var value = 0; value < histogram.Length; value++)
        {
            weightBackground += histogram[value];
            if (weightBackground == 0)
            {
                continue;
            }

            var weightForeground = total - weightBackground;
            if (weightForeground == 0)
            {
                break;
            }

            sumBackground += value * histogram[value];
            var meanBackground = sumBackground / (double)weightBackground;
            var meanForeground = (sum - sumBackground) / (double)weightForeground;
            var variance = weightBackground * weightForeground * Math.Pow(meanBackground - meanForeground, 2);

            if (variance > maxVariance)
            {
                maxVariance = variance;
                threshold = value;
            }
        }

        return threshold;
    }

    private static bool[,] BuildBinaryMask(byte[,] grayscale, int threshold, bool invert, CancellationToken cancellationToken)
    {
        var height = grayscale.GetLength(0);
        var width = grayscale.GetLength(1);
        var mask = new bool[height, width];

        for (var y = 0; y < height; y++)
        {
            if ((y & 31) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            for (var x = 0; x < width; x++)
            {
                var isForeground = grayscale[y, x] <= threshold;
                mask[y, x] = invert ? !isForeground : isForeground;
            }
        }

        return mask;
    }

    private static bool[,] BuildEdgeMask(bool[,] mask, string mode, CancellationToken cancellationToken)
    {
        var height = mask.GetLength(0);
        var width = mask.GetLength(1);
        var edges = new bool[height, width];

        var isSilhouetteMode = mode is "outer_silhouette" or "silhouette_with_holes";
        var isStrokeCenterlineMode = mode == "stroke_centerlines";

        for (var y = 0; y < height; y++)
        {
            if ((y & 31) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            for (var x = 0; x < width; x++)
            {
                if (!mask[y, x])
                {
                    continue;
                }

                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    edges[y, x] = true;
                    continue;
                }

                var boundary = !mask[y - 1, x] || !mask[y + 1, x] || !mask[y, x - 1] || !mask[y, x + 1];
                if (!boundary)
                {
                    continue;
                }

                if (isSilhouetteMode)
                {
                    if (TouchesBackgroundConnectedToFrame(mask, x, y))
                    {
                        edges[y, x] = true;
                    }

                    continue;
                }

                if (isStrokeCenterlineMode)
                {
                    var left = CountForeground(mask, x - 1, y, -1, 0, 10);
                    var right = CountForeground(mask, x + 1, y, 1, 0, 10);
                    var up = CountForeground(mask, x, y - 1, 0, -1, 10);
                    var down = CountForeground(mask, x, y + 1, 0, 1, 10);
                    edges[y, x] = Math.Abs(left - right) <= 1 || Math.Abs(up - down) <= 1;
                    continue;
                }

                edges[y, x] = true;
            }
        }

        return edges;
    }

    private static IReadOnlyList<PixelSegment> BuildSegments(bool[,] edges, double minSegmentLengthPx, int maxSegments, CancellationToken cancellationToken)
    {
        var height = edges.GetLength(0);
        var width = edges.GetLength(1);

        var allSegments = new List<PixelSegment>();

        for (var y = 0; y < height; y++)
        {
            if ((y & 31) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var runStart = -1;
            for (var x = 0; x < width; x++)
            {
                if (edges[y, x])
                {
                    runStart = runStart < 0 ? x : runStart;
                    continue;
                }

                if (runStart >= 0)
                {
                    TryAddSegment(allSegments, runStart, y, x - 1, y, minSegmentLengthPx);
                    runStart = -1;
                }
            }

            if (runStart >= 0)
            {
                TryAddSegment(allSegments, runStart, y, width - 1, y, minSegmentLengthPx);
            }
        }

        for (var x = 0; x < width; x++)
        {
            if ((x & 31) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var runStart = -1;
            for (var y = 0; y < height; y++)
            {
                if (edges[y, x])
                {
                    runStart = runStart < 0 ? y : runStart;
                    continue;
                }

                if (runStart >= 0)
                {
                    TryAddSegment(allSegments, x, runStart, x, y - 1, minSegmentLengthPx);
                    runStart = -1;
                }
            }

            if (runStart >= 0)
            {
                TryAddSegment(allSegments, x, runStart, x, height - 1, minSegmentLengthPx);
            }
        }

        var uniqueSegments = new List<PixelSegment>(allSegments.Count);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var segment in allSegments)
        {
            var key = $"{Math.Min(segment.X1, segment.X2)}:{Math.Min(segment.Y1, segment.Y2)}:{Math.Max(segment.X1, segment.X2)}:{Math.Max(segment.Y1, segment.Y2)}";
            if (keys.Add(key))
            {
                uniqueSegments.Add(segment);
            }
        }

        if (uniqueSegments.Count <= maxSegments)
        {
            return uniqueSegments;
        }

        return uniqueSegments
            .OrderByDescending(segment => segment.LengthPx)
            .ThenBy(segment => segment.X1)
            .ThenBy(segment => segment.Y1)
            .Take(maxSegments)
            .ToArray();
    }

    private static int CountTrue(bool[,] values)
    {
        var count = 0;
        var height = values.GetLength(0);
        var width = values.GetLength(1);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (values[y, x])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static Dictionary<string, object?> BuildQualityMetrics(TraceResult trace)
    {
        var imageArea = Math.Max(1, trace.Width * trace.Height);
        var reconstructed = RasterizeSegments(trace.Width, trace.Height, trace.Segments);
        var reconstructedCount = CountTrue(reconstructed);
        var overlap = Math.Min(trace.EdgePixelCount, reconstructedCount);
        var union = Math.Max(1, trace.EdgePixelCount + reconstructedCount - overlap);

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["edgePixelOccupancy"] = trace.EdgePixelCount / (double)imageArea,
            ["segmentDensity"] = trace.Segments.Count / (double)imageArea,
            ["reverseRasterIoU"] = overlap / (double)union,
            ["edgePixelCount"] = trace.EdgePixelCount,
            ["reconstructedPixelCount"] = reconstructedCount,
        };
    }

    private static bool[,] RasterizeSegments(int width, int height, IReadOnlyList<PixelSegment> segments)
    {
        var grid = new bool[height, width];

        foreach (var segment in segments)
        {
            if (segment.X1 == segment.X2)
            {
                var x = Math.Clamp(segment.X1, 0, width - 1);
                var yStart = Math.Clamp(Math.Min(segment.Y1, segment.Y2), 0, height - 1);
                var yEnd = Math.Clamp(Math.Max(segment.Y1, segment.Y2), 0, height - 1);
                for (var y = yStart; y <= yEnd; y++)
                {
                    grid[y, x] = true;
                }
            }
            else if (segment.Y1 == segment.Y2)
            {
                var y = Math.Clamp(segment.Y1, 0, height - 1);
                var xStart = Math.Clamp(Math.Min(segment.X1, segment.X2), 0, width - 1);
                var xEnd = Math.Clamp(Math.Max(segment.X1, segment.X2), 0, width - 1);
                for (var x = xStart; x <= xEnd; x++)
                {
                    grid[y, x] = true;
                }
            }
        }

        return grid;
    }

    private static int CountForeground(bool[,] mask, int startX, int startY, int stepX, int stepY, int limit)
    {
        var width = mask.GetLength(1);
        var height = mask.GetLength(0);
        var x = startX;
        var y = startY;
        var count = 0;

        for (var i = 0; i < limit; i++)
        {
            if (x < 0 || y < 0 || x >= width || y >= height || !mask[y, x])
            {
                break;
            }

            count++;
            x += stepX;
            y += stepY;
        }

        return count;
    }

    private static bool TouchesBackgroundConnectedToFrame(bool[,] mask, int x, int y)
    {
        var width = mask.GetLength(1);
        var height = mask.GetLength(0);

        ReadOnlySpan<(int dx, int dy)> neighbors = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        foreach (var (dx, dy) in neighbors)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
            {
                return true;
            }

            if (!mask[ny, nx] && (nx == 0 || ny == 0 || nx == width - 1 || ny == height - 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSuccessResult(object? result)
    {
        if (result is IDictionary<string, object?> dictionary && dictionary.TryGetValue("success", out var successValue))
        {
            try
            {
                return Convert.ToBoolean(successValue);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        return false;
    }

    private static void TryAddSegment(List<PixelSegment> segments, int x1, int y1, int x2, int y2, double minSegmentLengthPx)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length < minSegmentLengthPx)
        {
            return;
        }

        segments.Add(new PixelSegment(x1, y1, x2, y2, length));
    }

    private sealed record TraceOptions(
        int? Threshold,
        bool Invert,
        double MinSegmentLengthPx,
        int MaxSegments,
        string Mode,
        string Backend,
        string ResolvedBackend,
        bool QualityGate);

    private sealed record TraceResult(int Width, int Height, int Threshold, int EdgePixelCount, IReadOnlyList<PixelSegment> Segments);

    private sealed record PixelSegment(int X1, int Y1, int X2, int Y2, double LengthPx);
}
