namespace SolidworksMCP;

using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

public sealed class SolidWorksApi
{
    private const int SwRestore = 9;

    private readonly string? outputRoot;
    private object? swApp;
    private object? currentModel;
    private string? lastDrawingSourceModelPath;

    public SolidWorksApi()
        : this(null)
    {
    }

    public SolidWorksApi(string? outputRoot)
    {
        this.outputRoot = NormalizeOutputRoot(outputRoot);
    }

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out object? ppunk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    public bool IsConnected() => swApp is not null;

    public void Connect()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("SolidWorks COM automation is supported only on Windows.");
        }

        try
        {
            swApp = CreateSolidWorksApplication();
            SetVisible(swApp, true);
            TryBringApplicationToFront(swApp);
            AppLogger.Info("Connected to SolidWorks");
            return;
        }
        catch (Exception firstError)
        {
            try
            {
                swApp = CreateSolidWorksApplication();
                SetVisible(swApp, true);
                TryBringApplicationToFront(swApp);
                AppLogger.Info("Connected to SolidWorks (alternative method)");
            }
            catch (Exception secondError)
            {
                AppLogger.Error("Failed to connect to SolidWorks", new { firstError = firstError.Message, secondError = secondError.Message });
                throw new InvalidOperationException($"Failed to connect to SolidWorks: {secondError.Message}", secondError);
            }
        }
    }

    public string LaunchAndBringToFront()
    {
        EnsureConnected();

        SetVisible(swApp!, true);
        var broughtToFront = TryBringApplicationToFront(swApp!);
        return broughtToFront
            ? "SolidWorks instance is active and brought to front."
            : "SolidWorks instance is active, but Windows blocked foreground activation. Use Alt+Tab if needed.";
    }

    public void Disconnect()
    {
        currentModel = null;
        swApp = null;
    }

    public SolidWorksModel OpenModel(string filePath)
    {
        EnsureConnected();
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var docType = GetDocumentType(filePath);
        var errors = 0;
        var warnings = 0;
        currentModel = Invoke(swApp!, "OpenDoc6", filePath, docType, 1, string.Empty, errors, warnings) ?? throw new InvalidOperationException($"Failed to open model: {filePath}");

        TryInvoke(swApp!, "ActivateDoc2", GetString(currentModel, "GetTitle") ?? string.Empty, false, errors);
        lastDrawingSourceModelPath = filePath;

        return new SolidWorksModel
        {
            Path = filePath,
            Name = GetString(currentModel, "GetTitle") ?? Path.GetFileName(filePath),
            Type = docType switch
            {
                1 => "Part",
                2 => "Assembly",
                _ => "Drawing",
            },
            IsActive = true,
        };
    }

    public void CloseModel(bool save = false)
    {
        if (currentModel is null)
        {
            return;
        }

        var title = GetCurrentModelTitleOrPath();

        if (save)
        {
            var save3 = TryInvoke(currentModel, "Save3", 1, 0, 0);
            if (save3 is not bool saved || !saved)
            {
                _ = TryInvoke(currentModel, "Save");
            }
        }

        if (!string.IsNullOrWhiteSpace(title) && !string.Equals(title, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            TryInvoke(swApp!, "CloseDoc", title);
        }

        currentModel = null;
    }

    public SolidWorksModel CreatePart()
    {
        EnsureConnected();

        List<string> inspectedLocations = [];
        string? templatePathUsed = null;
        var templateSource = "solidworks-newpart-default";

        currentModel = Invoke(swApp!, "NewPart");
        if (currentModel is not null)
        {
            templatePathUsed = "<SolidWorks NewPart()>";
        }
        if (currentModel is null)
        {
            currentModel = TryCreatePartFromTemplate(swApp!, out inspectedLocations, out templatePathUsed);
            templateSource = "template-resolution";
        }

        if (currentModel is null)
        {
            var inspectedText = inspectedLocations.Count == 0
                ? "(no template paths were discovered)"
                : string.Join("; ", inspectedLocations);

            throw new InvalidOperationException($"Failed to create new part - no template available. Configure SOLIDWORKS_PART_TEMPLATE to a valid .prtdot file. Inspected: {inspectedText}");
        }

        lastDrawingSourceModelPath = null;

        return new SolidWorksModel
        {
            Path = string.Empty,
            Name = Convert.ToString(GetMethodValue(currentModel, "GetTitle")) ?? "Part",
            Type = "Part",
            IsActive = true,
            TemplatePath = templatePathUsed,
            TemplateSource = templateSource,
        };
    }

    public Dictionary<string, object?> ListPartTemplates()
    {
        EnsureConnected();

        var inspectedLocations = new List<string>();
        var templates = DiscoverPartTemplateCandidates(swApp!, inspectedLocations);

        var templateItems = templates
            .Select(path => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = path,
                ["name"] = Path.GetFileName(path),
                ["exists"] = File.Exists(path),
            })
            .Cast<object?>()
            .ToList();

        var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["defaultPartTemplate"] = Convert.ToString(TryInvoke(swApp!, "GetUserPreferenceStringValue", 13)),
            ["alternatePartTemplate"] = Convert.ToString(TryInvoke(swApp!, "GetUserPreferenceStringValue", 14)),
        };

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["templateCount"] = templateItems.Count,
            ["templates"] = templateItems,
            ["defaults"] = defaults,
            ["inspectedLocations"] = inspectedLocations.Distinct(StringComparer.OrdinalIgnoreCase).Cast<object?>().ToList(),
        };
    }

    public SolidWorksModel CreateAssembly()
    {
        EnsureConnected();

        List<string> inspectedLocations = [];
        string? templatePathUsed = null;
        var templateSource = "solidworks-newassembly-default";

        currentModel = Invoke(swApp!, "NewAssembly");
        if (currentModel is not null)
        {
            templatePathUsed = "<SolidWorks NewAssembly()>";
        }

        if (currentModel is null)
        {
            var templateCandidates = DiscoverTemplateCandidates(swApp!, inspectedLocations, "SOLIDWORKS_ASSEMBLY_TEMPLATE", TryGetAssemblyDocumentTemplate, [16, 17], ".asmdot");
            foreach (var templatePath in templateCandidates)
            {
                var expandedTemplatePath = Environment.ExpandEnvironmentVariables(templatePath);
                if (string.IsNullOrWhiteSpace(expandedTemplatePath))
                {
                    continue;
                }

                var (created, error) = TryCreateDocumentFromTemplate(swApp!, expandedTemplatePath);
                if (created is not null)
                {
                    currentModel = created;
                    templatePathUsed = expandedTemplatePath;
                    templateSource = "template-resolution";
                    break;
                }

                var existsState = File.Exists(expandedTemplatePath) ? "exists" : "missing";
                var errorSuffix = string.IsNullOrWhiteSpace(error) ? string.Empty : $" (error: {error})";
                inspectedLocations.Add($"<template failed to open ({existsState})> {expandedTemplatePath}{errorSuffix}");
            }
        }

        if (currentModel is null)
        {
            var inspectedText = inspectedLocations.Count == 0
                ? "(no assembly template paths were discovered)"
                : string.Join("; ", inspectedLocations);

            throw new InvalidOperationException($"Failed to create new assembly - no template available. Configure SOLIDWORKS_ASSEMBLY_TEMPLATE to a valid .asmdot file. Inspected: {inspectedText}");
        }

        lastDrawingSourceModelPath = null;

        return new SolidWorksModel
        {
            Path = string.Empty,
            Name = Convert.ToString(GetMethodValue(currentModel, "GetTitle")) ?? "Assembly",
            Type = "Assembly",
            IsActive = true,
            TemplatePath = templatePathUsed,
            TemplateSource = templateSource,
        };
    }

    public IReadOnlyList<EntityDescriptor> ListReferencePlanes()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        return EnumerateRecentFeatures(currentModel)
            .Where(IsPlaneFeature)
            .Select((feature, index) => new EntityDescriptor
            {
                Index = index,
                Kind = "plane",
                Name = GetFeatureName(feature) ?? $"Plane{index}",
                Type = Convert.ToString(GetMethodValue(feature, "GetTypeName2")),
                Handle = BuildEntityHandle("plane", GetFeatureName(feature) ?? $"Plane{index}")
            })
            .ToList();
    }

    public IReadOnlyList<EntityDescriptor> ListReferenceAxes()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        return EnumerateRecentFeatures(currentModel)
            .Where(IsAxisFeature)
            .Select((feature, index) => new EntityDescriptor
            {
                Index = index,
                Kind = "axis",
                Name = GetFeatureName(feature) ?? $"Axis{index}",
                Type = Convert.ToString(GetMethodValue(feature, "GetTypeName2")),
                Handle = BuildEntityHandle("axis", GetFeatureName(feature) ?? $"Axis{index}")
            })
            .ToList();
    }

    public Dictionary<string, object?> CreatePlane(string mode, SelectionSpec selection, double? distanceMm, double? angleDeg, bool flip, string? name)
    {
        EnsureCurrentModel();
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(selection);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        if (!IsPartDocument(currentModel))
        {
            throw new InvalidOperationException("Current document must be a part.");
        }

        _ = ExitSketch(false);
        var existingFeatures = CaptureRecentFeatureSignatures(currentModel);
        var references = ApplySelection(selection);
        var featureManager = InvokeProperty(currentModel, "FeatureManager") ?? throw new InvalidOperationException("FeatureManager unavailable.");
        var modeKey = mode.Trim().ToLowerInvariant();
        const int planeDistance = 8;
        const int planeAngle = 16;
        const int planeCoincident = 1;
        const int planeFlip = 256;
        const int planeMidplane = 4096;
        const int planeParallel = 4;
        var flipFlag = flip ? planeFlip : 0;

        var feature = modeKey switch
        {
            "offset" when distanceMm.HasValue => TryInvoke(featureManager, "InsertRefPlane", planeDistance | flipFlag, distanceMm.Value / 1000d, 0, 0d, 0, 0d),
            "angle" when angleDeg.HasValue => TryInvoke(featureManager, "InsertRefPlane", planeAngle | flipFlag, angleDeg.Value * Math.PI / 180d, planeCoincident, 0d, 0, 0d),
            "midplane" => TryInvoke(featureManager, "InsertRefPlane", planeMidplane, 0d, planeMidplane, 0d, 0, 0d),
            "three_points" => TryInvoke(featureManager, "InsertRefPlane", planeCoincident, 0d, planeCoincident, 0d, planeCoincident, 0d),
            "parallel_through_point" => TryInvoke(featureManager, "InsertRefPlane", planeParallel, 0d, planeCoincident, 0d, 0, 0d),
            _ => throw new InvalidOperationException($"Unsupported plane mode or missing inputs: {mode}"),
        };

        TryClearSelection();
        var resolvedFeature = ResolveCreatedFeature(currentModel, feature, existingFeatures);
        if (!string.IsNullOrWhiteSpace(name) && resolvedFeature is not null)
        {
            TryRenameFeature(resolvedFeature, name);
        }

        var featureName = GetFeatureName(resolvedFeature) ?? name ?? string.Empty;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = resolvedFeature is not null,
            ["feature"] = featureName,
            ["references"] = references,
            ["planes"] = ListReferencePlanes(),
            ["message"] = resolvedFeature is not null ? $"Created {modeKey} reference plane." : "SOLIDWORKS did not create a reference plane.",
        };
    }

    public Dictionary<string, object?> CreateAxis(SelectionSpec selection, string? name)
    {
        EnsureCurrentModel();
        ArgumentNullException.ThrowIfNull(selection);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        if (!IsPartDocument(currentModel))
        {
            throw new InvalidOperationException("Current document must be a part.");
        }

        _ = ExitSketch(false);
        var existingFeatures = CaptureRecentFeatureSignatures(currentModel);
        var references = ApplySelection(selection);
        var created = TryInvoke(currentModel, "InsertAxis2", true);
        TryClearSelection();
        var resolvedFeature = ResolveCreatedFeature(currentModel, created, existingFeatures);
        if (!string.IsNullOrWhiteSpace(name) && resolvedFeature is not null)
        {
            TryRenameFeature(resolvedFeature, name);
        }

        var featureName = GetFeatureName(resolvedFeature) ?? name ?? string.Empty;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = resolvedFeature is not null,
            ["feature"] = featureName,
            ["references"] = references,
            ["axes"] = ListReferenceAxes(),
            ["message"] = resolvedFeature is not null ? $"Created reference axis '{featureName}'." : "SOLIDWORKS could not build an axis from that selection.",
        };
    }

    public IReadOnlyList<EntityDescriptor> ListSketches()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        return EnumerateRecentFeatures(currentModel)
            .Where(IsSketchFeature)
            .Select((feature, index) => new EntityDescriptor
            {
                Index = index,
                Kind = "sketch",
                Name = GetFeatureName(feature) ?? $"Sketch{index}",
                Type = Convert.ToString(GetMethodValue(feature, "GetTypeName2")),
                Handle = BuildEntityHandle("sketch", GetFeatureName(feature) ?? $"Sketch{index}")
            })
            .ToList();
    }

    public IReadOnlyList<EntityDescriptor> ListSketchSegments(string? sketchName = null)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var sketch = ResolveSketch(sketchName) ?? throw new InvalidOperationException($"Sketch not found: {sketchName ?? "<active>"}");
        var specificSketch = TryInvoke(sketch, "GetSpecificFeature2") ?? sketch;
        var segments = AsEnumerable(TryInvoke(specificSketch, "GetSketchSegments") ?? TryInvoke(specificSketch, "GetSketchSegments2"));
        return segments
            .Select((segment, index) => new EntityDescriptor
            {
                Index = index,
                Kind = "sketch_segment",
                Name = Convert.ToString(GetMethodValue(segment, "GetNameForSelection")) ?? $"Segment{index}",
                Type = Convert.ToString(GetMethodValue(segment, "GetType")) ?? Convert.ToString(GetMethodValue(segment, "GetTypeName2")),
                Handle = BuildEntityHandle("sketch_segment", $"{GetFeatureName(sketch) ?? sketchName ?? "Sketch"}:{index}")
            })
            .ToList();
    }

    public IReadOnlyList<SolidWorksDimension> ListDimensions(string? ownerName = null)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var dimensions = new List<SolidWorksDimension>();
        foreach (var feature in EnumerateRecentFeatures(currentModel))
        {
            var featureName = GetFeatureName(feature);
            if (!string.IsNullOrWhiteSpace(ownerName) && !string.Equals(featureName, ownerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dimension = TryInvoke(feature, "GetFirstDisplayDimension") ?? TryInvoke(feature, "GetFirstDimension");
            var safety = 0;
            while (dimension is not null && safety++ < 100)
            {
                var dimensionObject = TryInvoke(dimension, "GetDimension2", 0) ?? TryInvoke(dimension, "GetDimension");
                var name = GetString(dimensionObject, "FullName") ?? GetString(dimensionObject, "Name") ?? GetString(dimension, "GetNameForSelection");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var value = GetNumberFromObject(dimensionObject ?? dimension, "SystemValue", double.NaN);
                    if (double.IsNaN(value))
                    {
                        value = GetNumberFromObject(dimensionObject ?? dimension, "Value", 0d);
                    }

                    dimensions.Add(new SolidWorksDimension
                    {
                        Name = name,
                        Value = value * 1000d,
                        Units = "mm",
                    });
                }

                dimension = TryInvoke(dimension, "GetNext3") ?? TryInvoke(dimension, "GetNext");
            }
        }

        return dimensions;
    }

    public IReadOnlyList<Dictionary<string, object?>> ListBodies()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var bodies = GetModelBodies(currentModel);
        return bodies.Select((body, index) =>
        {
            var box = TryGetBodyBox(body);
            var name = GetString(body, "Name") ?? $"body{index}";
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["index"] = index,
                ["name"] = name,
                ["handle"] = BuildEntityHandle("body", name),
            };

            if (box.Length >= 6)
            {
                result["minMm"] = CreatePointDictionary(box[0] * 1000d, box[1] * 1000d, box[2] * 1000d);
                result["maxMm"] = CreatePointDictionary(box[3] * 1000d, box[4] * 1000d, box[5] * 1000d);
            }

            return result;
        }).ToList();
    }

    public IReadOnlyList<Dictionary<string, object?>> ListFaces(string? surfaceType = null, double? minAreaMm2 = null)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var faces = new List<Dictionary<string, object?>>();
        var index = 0;
        foreach (var body in GetModelBodies(currentModel))
        {
            foreach (var face in AsEnumerable(TryInvoke(body, "GetFaces")))
            {
                var faceType = NormalizeSurfaceType(Convert.ToInt32(TryInvoke(TryInvoke(face, "GetSurface"), "Identity") ?? -1));
                var areaMm2 = GetNumberFromMethod(face, "GetArea", 0d) * 1_000_000d;
                if (!string.IsNullOrWhiteSpace(surfaceType) && !string.Equals(faceType, surfaceType, StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    continue;
                }

                if (minAreaMm2.HasValue && areaMm2 < minAreaMm2.Value)
                {
                    index++;
                    continue;
                }

                var point = GetFacePickPoint(face);
                faces.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["index"] = index,
                    ["surfaceType"] = faceType,
                    ["areaMm2"] = Math.Round(areaMm2, 6),
                    ["pointMm"] = CreatePointDictionary(point.X * 1000d, point.Y * 1000d, point.Z * 1000d),
                    ["handle"] = BuildEntityHandle("face", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                });
                index++;
            }
        }

        return faces;
    }

    public IReadOnlyList<Dictionary<string, object?>> ListEdges(string? curveType = null, double? minLengthMm = null, double? maxLengthMm = null)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var edges = new List<Dictionary<string, object?>>();
        var index = 0;
        foreach (var body in GetModelBodies(currentModel))
        {
            foreach (var edge in AsEnumerable(TryInvoke(body, "GetEdges")))
            {
                var resolvedCurveType = NormalizeCurveType(Convert.ToInt32(TryInvoke(TryInvoke(edge, "GetCurve"), "Identity") ?? -1));
                var lengthMm = GetNumberFromMethod(edge, "GetLength", 0d) * 1000d;
                if (!string.IsNullOrWhiteSpace(curveType) && !string.Equals(resolvedCurveType, curveType, StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    continue;
                }

                if (minLengthMm.HasValue && lengthMm < minLengthMm.Value)
                {
                    index++;
                    continue;
                }

                if (maxLengthMm.HasValue && lengthMm > maxLengthMm.Value)
                {
                    index++;
                    continue;
                }

                edges.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["index"] = index,
                    ["curveType"] = resolvedCurveType,
                    ["lengthMm"] = Math.Round(lengthMm, 6),
                    ["handle"] = BuildEntityHandle("edge", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                });
                index++;
            }
        }

        return edges;
    }

    public IReadOnlyList<Dictionary<string, object?>> ListVertices()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var vertices = new List<Dictionary<string, object?>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var body in GetModelBodies(currentModel))
        {
            foreach (var edge in AsEnumerable(TryInvoke(body, "GetEdges")))
            {
                foreach (var member in new[] { "GetStartVertex", "GetEndVertex" })
                {
                    var vertex = TryInvoke(edge, member);
                    var pointArray = vertex is null ? [] : GetArrayFromObject(vertex, "Point");
                    if (pointArray.Length < 3)
                    {
                        continue;
                    }

                    var key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Math.Round(pointArray[0], 9)}|{Math.Round(pointArray[1], 9)}|{Math.Round(pointArray[2], 9)}");
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    vertices.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["index"] = index,
                        ["pointMm"] = CreatePointDictionary(pointArray[0] * 1000d, pointArray[1] * 1000d, pointArray[2] * 1000d),
                        ["handle"] = BuildEntityHandle("vertex", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    });
                    index++;
                }
            }
        }

        return vertices;
    }

    public IReadOnlyList<Dictionary<string, object?>> ListFeatures()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        return EnumerateRecentFeatures(currentModel, 500)
            .Select((feature, index) => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["index"] = index,
                ["name"] = GetFeatureName(feature) ?? $"Feature{index}",
                ["type"] = Convert.ToString(GetMethodValue(feature, "GetTypeName2")) ?? string.Empty,
                ["suppressed"] = Convert.ToBoolean(TryInvoke(feature, "IsSuppressed2", 0, null) ?? TryInvoke(feature, "IsSuppressed") ?? false),
                ["handle"] = BuildEntityHandle("feature", GetFeatureName(feature) ?? $"Feature{index}"),
            })
            .ToList();
    }

    public Dictionary<string, object?> GetBoundingBox()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var box = GetSolidWorksDocumentType(currentModel) == 1
            ? GetArrayFromMethod(currentModel, "GetPartBox", true)
            : GetArrayFromMethod(currentModel, "GetBox", 0);
        if (box.Length < 6)
        {
            throw new InvalidOperationException("The active document has no geometry to bound.");
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["minMm"] = CreatePointDictionary(box[0] * 1000d, box[1] * 1000d, box[2] * 1000d),
            ["maxMm"] = CreatePointDictionary(box[3] * 1000d, box[4] * 1000d, box[5] * 1000d),
            ["sizeMm"] = new[]
            {
                Math.Round((box[3] - box[0]) * 1000d, 6),
                Math.Round((box[4] - box[1]) * 1000d, 6),
                Math.Round((box[5] - box[2]) * 1000d, 6),
            },
        };
    }

    public Dictionary<string, object?> MeasureSelection(SelectionSpec selection)
    {
        EnsureCurrentModel();
        ArgumentNullException.ThrowIfNull(selection);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var count = ApplySelection(selection);
        if (count == 0)
        {
            throw new InvalidOperationException("The selection specification did not resolve to anything to measure.");
        }

        var extension = GetProperty(currentModel, "Extension") ?? throw new InvalidOperationException("Model extension is unavailable.");
        var measure = GetProperty(extension, "CreateMeasure") ?? TryInvoke(extension, "CreateMeasure") ?? throw new InvalidOperationException("SOLIDWORKS did not provide a measure object.");
        _ = TrySetProperty(measure, "ArcOption", 0);
        var calculated = GetProperty(measure, "Calculate") ?? TryInvoke(measure, "Calculate");
        if (calculated is not bool succeeded || !succeeded)
        {
            TryClearSelection();
            throw new InvalidOperationException("SOLIDWORKS could not measure that selection.");
        }

        try
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["entities"] = count,
                ["lengthMm"] = ReadMeasureValue(measure, "Length", 1000d),
                ["totalLengthMm"] = ReadMeasureValue(measure, "TotalLength", 1000d),
                ["arcLengthMm"] = ReadMeasureValue(measure, "ArcLength", 1000d),
                ["chordLengthMm"] = ReadMeasureValue(measure, "ChordLength", 1000d),
                ["perimeterMm"] = ReadMeasureValue(measure, "Perimeter", 1000d),
                ["areaMm2"] = ReadMeasureValue(measure, "Area", 1_000_000d),
                ["totalAreaMm2"] = ReadMeasureValue(measure, "TotalArea", 1_000_000d),
                ["distanceMm"] = ReadMeasureValue(measure, "Distance", 1000d),
                ["normalDistanceMm"] = ReadMeasureValue(measure, "NormalDistance", 1000d),
                ["centerDistanceMm"] = ReadMeasureValue(measure, "CenterDistance", 1000d),
                ["deltaXmm"] = ReadMeasureValue(measure, "DeltaX", 1000d),
                ["deltaYmm"] = ReadMeasureValue(measure, "DeltaY", 1000d),
                ["deltaZmm"] = ReadMeasureValue(measure, "DeltaZ", 1000d),
                ["angleDeg"] = ReadMeasureValue(measure, "Angle", 180d / Math.PI),
                ["diameterMm"] = ReadMeasureValue(measure, "Diameter", 1000d),
                ["radiusMm"] = ReadMeasureValue(measure, "Radius", 1000d),
            }.Where(pair => pair.Value is not null).ToDictionary();
        }
        finally
        {
            TryClearSelection();
        }
    }

    public IReadOnlyList<Dictionary<string, object?>> ListMates()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        if (!IsAssemblyDocument(currentModel))
        {
            throw new InvalidOperationException("Current document must be an assembly.");
        }

        var mateTypeNames = new Dictionary<int, string>
        {
            [0] = "coincident",
            [1] = "concentric",
            [3] = "parallel",
            [4] = "perpendicular",
            [5] = "distance",
            [6] = "angle",
            [8] = "tangent",
            [12] = "lock",
        };

        var mates = new List<Dictionary<string, object?>>();
        foreach (var feature in EnumerateRecentFeatures(currentModel, 500))
        {
            if (!string.Equals(Convert.ToString(GetMethodValue(feature, "GetTypeName2")), "MateGroup", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var child = TryInvoke(feature, "GetFirstSubFeature");
            while (child is not null)
            {
                var definition = TryInvoke(child, "GetSpecificFeature2");
                var typeCode = Convert.ToInt32(GetProperty(definition, "Type") ?? -1);
                var distance = GetNumberFromObject(definition, "Distance", double.NaN);
                var angle = GetNumberFromObject(definition, "Angle", double.NaN);
                mates.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = GetFeatureName(child) ?? GetString(child, "Name") ?? string.Empty,
                    ["mateType"] = mateTypeNames.TryGetValue(typeCode, out var mateTypeName) ? mateTypeName : $"type_{typeCode}",
                    ["distanceMm"] = double.IsNaN(distance) ? null : Math.Round(distance * 1000d, 6),
                    ["angleDeg"] = double.IsNaN(angle) ? null : Math.Round(angle * 180d / Math.PI, 6),
                });
                child = TryInvoke(child, "GetNextSubFeature");
            }
        }

        return mates;
    }

    public string GetSketchStatus(string? sketchName = null)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var sketch = ResolveSketch(sketchName);
        if (sketch is null)
        {
            return "closed";
        }

        var specificSketch = TryInvoke(sketch, "GetSpecificFeature2") ?? sketch;
        var statusCode = Convert.ToInt32(GetMethodValue(specificSketch, "GetConstrainedStatus") ?? GetMethodValue(sketch, "GetConstrainedStatus") ?? 0);
        return statusCode switch
        {
            1 => "unknown",
            2 => "under_defined",
            3 => "fully_defined",
            4 => "over_defined",
            5 => "no_solution",
            6 => "invalid_solution",
            7 => "autosolve_off",
            _ => statusCode == 0 ? "closed" : $"status_{statusCode}",
        };
    }

    public Dictionary<string, object?> AddSketchRelation(string relation, SelectionSpec selection)
    {
        EnsureCurrentModel();
        ArgumentException.ThrowIfNullOrWhiteSpace(relation);
        ArgumentNullException.ThrowIfNull(selection);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var sketch = GetActiveSketch(currentModel) ?? throw new InvalidOperationException("No sketch is open. Create or edit a sketch first.");
        var specificSketch = TryInvoke(sketch, "GetSpecificFeature2") ?? sketch;
        var relationManager = GetProperty(specificSketch, "RelationManager") ?? GetProperty(sketch, "RelationManager") ?? TryInvoke(specificSketch, "RelationManager") ?? TryInvoke(sketch, "RelationManager");
        if (relationManager is null)
        {
            throw new InvalidOperationException("Sketch relation manager is unavailable.");
        }

        var relationCode = relation.Trim().ToLowerInvariant() switch
        {
            "horizontal" => 4,
            "vertical" => 5,
            "tangent" => 6,
            "parallel" => 7,
            "perpendicular" => 8,
            "coincident" => 9,
            "concentric" => 10,
            "symmetric" => 11,
            "midpoint" => 12,
            "intersection" => 13,
            "equal" => 14,
            "fixed" => 17,
            "collinear" => 27,
            "coradial" => 28,
            _ => throw new ArgumentException($"Unsupported relation: {relation}", nameof(relation)),
        };

        var entities = ResolveSketchRelationEntities(selection).ToArray();
        if (entities.Length == 0)
        {
            throw new InvalidOperationException("Relation selection must include sketch_segments or sketch_points.");
        }

        var beforeCount = Convert.ToInt32(TryInvoke(relationManager, "GetRelationsCount", 0) ?? 0);
        try
        {
            TryInvoke(relationManager, "AddRelation", entities, relationCode);
        }
        finally
        {
            TryClearSelection();
        }

        var afterCount = Convert.ToInt32(TryInvoke(relationManager, "GetRelationsCount", 0) ?? beforeCount);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = afterCount > beforeCount,
            ["relation"] = relation,
            ["entities"] = entities.Length,
            ["relationsAdded"] = Math.Max(afterCount - beforeCount, 0),
            ["sketchStatus"] = GetSketchStatus(selection.SketchName),
            ["message"] = afterCount > beforeCount
                ? $"Added {relation} relation across {entities.Length} sketch entities."
                : $"SOLIDWORKS did not add a {relation} relation to that sketch selection.",
        };
    }

    public Dictionary<string, object?> AddSketchDimension(SelectionSpec selection, double? valueMm, double? valueDeg, string kind, double placeXmm, double placeYmm, double placeZmm)
    {
        EnsureCurrentModel();
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var sketchManager = InvokeProperty(currentModel, "SketchManager") ?? throw new InvalidOperationException("SketchManager unavailable");
        _ = GetActiveSketch(currentModel) ?? throw new InvalidOperationException("No sketch is open. Create or edit a sketch first.");
        var entities = ResolveSketchRelationEntities(selection).ToArray();
        if (entities.Length == 0)
        {
            throw new InvalidOperationException("Dimension selection must include sketch_segments or sketch_points.");
        }

        var selected = 0;
        foreach (var entity in entities)
        {
            var appended = selected > 0;
            if (!TrySelectEntity(entity, appended))
            {
                throw new InvalidOperationException("Failed to select one or more sketch entities for dimensioning.");
            }

            selected++;
        }

        var methodName = kind.Trim().ToLowerInvariant() switch
        {
            "auto" => "AddDimension2",
            "horizontal" => "AddHorizontalDimension2",
            "vertical" => "AddVerticalDimension2",
            "radius" => "AddRadialDimension2",
            "diameter" => "AddDiameterDimension2",
            _ => throw new ArgumentException($"Unsupported dimension kind: {kind}", nameof(kind)),
        };

        object? created;
        try
        {
            created = TryInvoke(sketchManager, methodName, placeXmm / 1000d, placeYmm / 1000d, placeZmm / 1000d);
        }
        finally
        {
            TryClearSelection();
        }

        if (created is null)
        {
            throw new InvalidOperationException($"SOLIDWORKS did not create a {kind} dimension for that sketch selection.");
        }

        var dimension = TryInvoke(created, "GetDimension2", 0) ?? TryInvoke(created, "GetDimension") ?? created;
        if (valueMm.HasValue)
        {
            SetDimensionValueCore(dimension, valueMm.Value / 1000d);
        }
        else if (valueDeg.HasValue)
        {
            SetDimensionValueCore(dimension, valueDeg.Value * Math.PI / 180d);
        }

        TryInvoke(currentModel, "EditRebuild3");
        var name = GetString(dimension, "FullName") ?? GetString(dimension, "Name") ?? GetString(created, "GetNameForSelection") ?? "<unnamed>";
        var rawValue = GetNumberFromObject(dimension, "SystemValue", double.NaN);
        var reportedUnits = valueDeg.HasValue && !valueMm.HasValue ? "deg" : "mm";
        var reportedValue = valueDeg.HasValue && !valueMm.HasValue
            ? (double.IsNaN(rawValue) ? valueDeg.Value : rawValue * 180d / Math.PI)
            : (double.IsNaN(rawValue) ? (valueMm ?? 0d) : rawValue * 1000d);

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["name"] = name,
            ["kind"] = kind,
            ["value"] = reportedValue,
            ["units"] = reportedUnits,
            ["sketchStatus"] = GetSketchStatus(selection.SketchName),
            ["message"] = $"Created sketch dimension {name}.",
        };
    }

    public IReadOnlyList<Dictionary<string, object?>> ListComponents(bool topLevelOnly)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        if (!IsAssemblyDocument(currentModel))
        {
            throw new InvalidOperationException("Current document must be an assembly.");
        }

        var rawComponents = topLevelOnly
            ? TryInvoke(currentModel, "GetComponents", false)
            : TryInvoke(currentModel, "GetComponents", true);
        var components = AsEnumerable(rawComponents);

        return components.Select((component, index) =>
        {
            var name = GetString(component, "Name2") ?? $"Component{index}";
            var transform = TryInvoke(component, "Transform2") ?? TryInvoke(component, "GetTotalTransform");
            var origin = ExtractTransformOrigin(transform);
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["index"] = index,
                ["name"] = name,
                ["path"] = GetString(component, "GetPathName") ?? string.Empty,
                ["suppressed"] = Convert.ToBoolean(TryInvoke(component, "IsSuppressed") ?? false),
                ["fixed"] = Convert.ToBoolean(TryInvoke(component, "IsFixed") ?? false),
                ["originMm"] = origin,
                ["handle"] = BuildEntityHandle("component", name),
            };
        }).ToList();
    }

    public Dictionary<string, object?> InsertComponent(string path, double xMm, double yMm, double zMm, string? configuration)
    {
        EnsureCurrentModel();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        if (!IsAssemblyDocument(currentModel))
        {
            throw new InvalidOperationException("Current document must be an assembly.");
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"No such file: {fullPath}", fullPath);
        }

        var docType = GetDocumentType(fullPath);
        if (docType is not (1 or 2))
        {
            throw new InvalidOperationException("Only part and assembly files can be inserted as components.");
        }

        _ = TryInvoke(swApp!, "OpenDoc6", fullPath, docType, 1, configuration ?? string.Empty, 0, 0);
        var component = TryInvoke(currentModel, "AddComponent5", fullPath, 0, string.Empty, false, configuration ?? string.Empty, xMm / 1000d, yMm / 1000d, zMm / 1000d)
            ?? TryInvoke(currentModel, "AddComponent", fullPath, xMm / 1000d, yMm / 1000d, zMm / 1000d);
        if (component is null)
        {
            throw new InvalidOperationException($"SOLIDWORKS did not insert {Path.GetFileName(fullPath)} into the assembly.");
        }

        var rebuild = RebuildModel(false);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["component"] = GetString(component, "Name2") ?? Path.GetFileNameWithoutExtension(fullPath),
            ["path"] = fullPath,
            ["rebuild"] = rebuild,
            ["message"] = $"Inserted {Path.GetFileName(fullPath)}.",
        };
    }

    public Dictionary<string, object?> SetComponentFixed(IReadOnlyList<string> componentNames, bool fixedState)
    {
        EnsureCurrentModel();
        ArgumentNullException.ThrowIfNull(componentNames);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        if (!IsAssemblyDocument(currentModel))
        {
            throw new InvalidOperationException("Current document must be an assembly.");
        }

        if (componentNames.Count == 0)
        {
            throw new InvalidOperationException("At least one component name is required.");
        }

        TryClearSelection();
        var selected = 0;
        foreach (var componentName in componentNames)
        {
            var component = ResolveAssemblyComponent(componentName) ?? throw new InvalidOperationException($"Component not found: {componentName}");
            if (!TrySelectEntity(component, selected > 0))
            {
                throw new InvalidOperationException($"Failed to select component: {componentName}");
            }

            selected++;
        }

        try
        {
            var actionResult = fixedState
                ? TryInvoke(currentModel, "FixComponent")
                : TryInvoke(currentModel, "UnfixComponent");
            if (actionResult is bool succeeded && !succeeded)
            {
                throw new InvalidOperationException("SOLIDWORKS rejected the component fixed-state update.");
            }
        }
        finally
        {
            TryClearSelection();
        }

        var rebuild = RebuildModel(false);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["fixed"] = fixedState,
            ["components"] = componentNames.Cast<object?>().ToList(),
            ["rebuild"] = rebuild,
            ["message"] = $"Set fixed={fixedState} on {componentNames.Count} component(s).",
        };
    }

    public Dictionary<string, object?> AddMate(SelectionSpec selection, string mateType, string alignment, double distanceMm, double angleDeg, bool flip, bool lockRotation)
    {
        EnsureCurrentModel();
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentException.ThrowIfNullOrWhiteSpace(mateType);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        if (!IsAssemblyDocument(currentModel))
        {
            throw new InvalidOperationException("Current document must be an assembly.");
        }

        var mateTypeCode = mateType.Trim().ToLowerInvariant() switch
        {
            "coincident" => 0,
            "concentric" => 1,
            "distance" => 5,
            "angle" => 6,
            "parallel" => 3,
            "perpendicular" => 4,
            "tangent" => 8,
            "lock" => 12,
            _ => throw new ArgumentException($"Unsupported mate type: {mateType}", nameof(mateType)),
        };

        var alignmentCode = alignment.Trim().ToLowerInvariant() switch
        {
            "closest" => 0,
            "aligned" => 1,
            "anti_aligned" => 2,
            _ => throw new ArgumentException($"Unsupported mate alignment: {alignment}", nameof(alignment)),
        };

        var selectedCount = ApplySelection(selection);
        if (selectedCount < 2)
        {
            TryClearSelection();
            throw new InvalidOperationException("Mate selection must resolve to at least two entities.");
        }

        object? mate;
        var status = 0;
        try
        {
            mate = TryInvoke(currentModel, "AddMate5", mateTypeCode, alignmentCode, flip, distanceMm / 1000d, distanceMm / 1000d, distanceMm / 1000d, 1.0d, 1.0d, angleDeg * Math.PI / 180d, angleDeg * Math.PI / 180d, angleDeg * Math.PI / 180d, false, lockRotation, 0, status)
                ?? TryInvoke(currentModel, "AddMate3", mateTypeCode, alignmentCode, flip, distanceMm / 1000d, 0d, angleDeg * Math.PI / 180d);
        }
        finally
        {
            TryClearSelection();
        }

        var rebuild = RebuildModel(false);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = mate is not null,
            ["mate"] = GetString(mate, "Name") ?? GetString(mate, "Name2") ?? string.Empty,
            ["mateType"] = mateType,
            ["alignment"] = alignment,
            ["selectedEntities"] = selectedCount,
            ["status"] = status,
            ["rebuild"] = rebuild,
            ["message"] = mate is not null
                ? $"Added a {mateType} mate."
                : $"SOLIDWORKS refused the {mateType} mate.",
        };
    }

    public Dictionary<string, object?> GetRebuildStatus(bool force)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var rebuild = RebuildModel(force);
        var errors = Convert.ToInt32(Invoke(currentModel, "GetErrorCode2") ?? Invoke(currentModel, "GetLastError") ?? 0);
        var warnings = Convert.ToInt32(Invoke(currentModel, "GetWarningCount") ?? 0);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = errors == 0,
            ["document"] = GetCurrentModelTitleOrPath(),
            ["rebuild"] = rebuild,
            ["errorCode"] = errors,
            ["warningCount"] = warnings,
            ["message"] = errors == 0 ? "Rebuilt the active document." : "The rebuild reported problems.",
        };
    }

    public Dictionary<string, object?> EditSketch(string sketchName)
    {
        EnsureCurrentModel();
        ArgumentException.ThrowIfNullOrWhiteSpace(sketchName);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var sketch = ResolveSketch(sketchName) ?? throw new InvalidOperationException($"Sketch not found: {sketchName}");
        TryClearSelection();
        if (!TrySelectSketchForExtrusion(currentModel, sketch))
        {
            throw new InvalidOperationException($"Could not select sketch '{sketchName}'.");
        }

        var sketchManager = InvokeProperty(currentModel, "SketchManager") ?? throw new InvalidOperationException("SketchManager unavailable");
        _ = TryInvoke(sketchManager, "InsertSketch", true);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["sketch"] = GetFeatureName(sketch) ?? sketchName,
            ["message"] = $"Reopened sketch '{sketchName}' for editing.",
        };
    }

    public Dictionary<string, object?> CloseSketch()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var sketchManager = InvokeProperty(currentModel, "SketchManager") ?? throw new InvalidOperationException("SketchManager unavailable");
        var activeSketch = GetProperty(sketchManager, "ActiveSketch") ?? TryInvoke(sketchManager, "GetActiveSketch2");
        if (activeSketch is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["message"] = "No sketch is open.",
            };
        }

        var sketchName = GetFeatureName(activeSketch) ?? GetString(activeSketch, "Name") ?? string.Empty;
        _ = TryInvoke(sketchManager, "InsertSketch", true);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["sketch"] = sketchName,
            ["message"] = "Closed the open sketch.",
        };
    }

    public Dictionary<string, object?> AddCenterLine(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        var x1 = GetNumber(parameters, "x1", 0);
        var y1 = GetNumber(parameters, "y1", 0);
        var z1 = GetNumber(parameters, "z1", 0);
        var x2 = GetNumber(parameters, "x2", 100);
        var y2 = GetNumber(parameters, "y2", 0);
        var z2 = GetNumber(parameters, "z2", 0);
        var sketchManager = InvokeProperty(currentModel!, "SketchManager") ?? throw new InvalidOperationException("SketchManager unavailable");
        var segment = TryInvoke(sketchManager, "CreateCenterLine", x1 / 1000d, y1 / 1000d, z1 / 1000d, x2 / 1000d, y2 / 1000d, z2 / 1000d);
        return segment is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = false, ["error"] = "Failed to create centerline" }
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = true, ["centerLineId"] = $"centerline_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" };
    }

    public Dictionary<string, object?> AddPoint(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        var x = GetNumber(parameters, "x", 0) / 1000d;
        var y = GetNumber(parameters, "y", 0) / 1000d;
        var z = GetNumber(parameters, "z", 0) / 1000d;
        var sketchManager = InvokeProperty(currentModel!, "SketchManager") ?? throw new InvalidOperationException("SketchManager unavailable");
        var point = TryInvoke(sketchManager, "CreatePoint", x, y, z);
        return point is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = false, ["error"] = "Failed to create point" }
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = true, ["pointId"] = $"point_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" };
    }

    public Dictionary<string, object?> AddArc(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        var sketchManager = InvokeProperty(currentModel!, "SketchManager") ?? throw new InvalidOperationException("SketchManager unavailable");
        var created = TryInvoke(
            sketchManager,
            "CreateArc",
            GetNumber(parameters, "centerX", 0) / 1000d,
            GetNumber(parameters, "centerY", 0) / 1000d,
            GetNumber(parameters, "centerZ", 0) / 1000d,
            GetNumber(parameters, "startX", 0) / 1000d,
            GetNumber(parameters, "startY", 0) / 1000d,
            GetNumber(parameters, "startZ", 0) / 1000d,
            GetNumber(parameters, "endX", 0) / 1000d,
            GetNumber(parameters, "endY", 0) / 1000d,
            GetNumber(parameters, "endZ", 0) / 1000d,
            GetBool(parameters, "clockwise", false));
        return created is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = false, ["error"] = "Failed to create arc" }
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = true, ["arcId"] = $"arc_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" };
    }

    public Dictionary<string, object?> Add3PointArc(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        var sketchManager = InvokeProperty(currentModel!, "SketchManager") ?? throw new InvalidOperationException("SketchManager unavailable");
        var created = TryInvoke(
            sketchManager,
            "Create3PointArc",
            GetNumber(parameters, "x1", 0) / 1000d,
            GetNumber(parameters, "y1", 0) / 1000d,
            GetNumber(parameters, "z1", 0) / 1000d,
            GetNumber(parameters, "x2", 0) / 1000d,
            GetNumber(parameters, "y2", 0) / 1000d,
            GetNumber(parameters, "z2", 0) / 1000d,
            GetNumber(parameters, "x3", 0) / 1000d,
            GetNumber(parameters, "y3", 0) / 1000d,
            GetNumber(parameters, "z3", 0) / 1000d);
        return created is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = false, ["error"] = "Failed to create 3-point arc" }
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = true, ["arcId"] = $"arc3_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" };
    }

    public Dictionary<string, object?> AddEllipse(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        var sketchManager = InvokeProperty(currentModel!, "SketchManager") ?? throw new InvalidOperationException("SketchManager unavailable");
        var created = TryInvoke(
            sketchManager,
            "CreateEllipse",
            GetNumber(parameters, "centerX", 0) / 1000d,
            GetNumber(parameters, "centerY", 0) / 1000d,
            0d,
            GetNumber(parameters, "majorX", 10) / 1000d,
            GetNumber(parameters, "majorY", 0) / 1000d,
            0d,
            GetNumber(parameters, "minorX", 0) / 1000d,
            GetNumber(parameters, "minorY", 5) / 1000d,
            0d);
        return created is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = false, ["error"] = "Failed to create ellipse" }
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["success"] = true, ["ellipseId"] = $"ellipse_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" };
    }

    public Dictionary<string, object?> SetConstructionGeometry(string? sketchName, IReadOnlyList<int> segmentIndices, bool construction)
    {
        EnsureCurrentModel();
        ArgumentNullException.ThrowIfNull(segmentIndices);
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var sketch = ResolveSketch(sketchName) ?? throw new InvalidOperationException($"Sketch not found: {sketchName ?? "<active>"}");
        var specificSketch = TryInvoke(sketch, "GetSpecificFeature2") ?? sketch;
        var segments = AsEnumerable(TryInvoke(specificSketch, "GetSketchSegments") ?? TryInvoke(specificSketch, "GetSketchSegments2"));
        var changed = 0;
        foreach (var index in segmentIndices)
        {
            if (index < 0 || index >= segments.Count)
            {
                throw new InvalidOperationException($"Sketch segment index {index} is out of range (0..{segments.Count - 1}).");
            }

            TrySetProperty(segments[index], "ConstructionGeometry", construction);
            changed++;
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["changed"] = changed,
            ["construction"] = construction,
            ["message"] = $"Set construction={construction} on {changed} segments.",
        };
    }

    public object CreateSketch(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        var debugTrace = CreateDevelopmentDebugTrace();

        if (currentModel is null)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "No active model",
            };

            AddDebugTrace(debugTrace, "EnsureCurrentModel did not resolve an active model.");
            AttachDebugTrace(result, debugTrace);
            return result;
        }

        var plane = parameters.TryGetValue("plane", out var planeValue) ? Convert.ToString(planeValue) : "Front";
        AddDebugTrace(debugTrace, $"Requested plane: {plane ?? "Front"}");
        AddDebugTrace(debugTrace, $"Active model type: {currentModel.GetType().FullName}");

        var sketchManager = InvokeProperty(currentModel, "SketchManager");
        if (sketchManager is null)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "SketchManager unavailable",
            };

            AddDebugTrace(debugTrace, "SketchManager property returned null via COM/reflection.");
            AttachDebugTrace(result, debugTrace);
            return result;
        }

        AddDebugTrace(debugTrace, $"SketchManager type: {sketchManager.GetType().FullName}");
        TryClearSelection();
        if (!TrySelectSketchPlane(currentModel, plane, debugTrace))
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = $"Failed to select sketch plane '{plane ?? "Front"}'",
            };

            AttachDebugTrace(result, debugTrace);
            return result;
        }

        _ = TryInvoke(sketchManager, "InsertSketch", true);
        AddDebugTrace(debugTrace, "Invoked InsertSketch(true).");
        var sketch = GetProperty(sketchManager, "ActiveSketch") ?? TryInvoke(sketchManager, "GetActiveSketch2");
        if (sketch is null)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "Failed to create sketch",
            };

            AddDebugTrace(debugTrace, "ActiveSketch/GetActiveSketch2 both returned null after InsertSketch.");
            AttachDebugTrace(result, debugTrace);
            return result;
        }

        var successResult = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["sketchId"] = GetString(sketch, "Name") ?? "Sketch",
            ["plane"] = plane ?? "Front",
        };

        AddDebugTrace(debugTrace, $"Sketch created: {GetString(sketch, "Name") ?? "Sketch"}");
        AttachDebugTrace(successResult, debugTrace);
        return successResult;
    }

    public object AddLine(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        var x1 = GetNumber(parameters, "x1", 0);
        var y1 = GetNumber(parameters, "y1", 0);
        var z1 = GetNumber(parameters, "z1", 0);
        var x2 = GetNumber(parameters, "x2", 100);
        var y2 = GetNumber(parameters, "y2", 0);
        var z2 = GetNumber(parameters, "z2", 0);

        var sketchManager = InvokeProperty(currentModel!, "SketchManager");
        var line = TryInvoke(sketchManager!, "CreateLine", x1 / 1000d, y1 / 1000d, z1 / 1000d, x2 / 1000d, y2 / 1000d, z2 / 1000d);
        if (line is not null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["lineId"] = $"line_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            };
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = false,
            ["error"] = "Failed to create line",
        };
    }

    public object AddCircle(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "No active model",
            };
        }

        var sketchManager = InvokeProperty(currentModel, "SketchManager");
        if (sketchManager is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "SketchManager unavailable",
            };
        }

        var centerX = GetNumber(parameters, "centerX", 0) / 1000d;
        var centerY = GetNumber(parameters, "centerY", 0) / 1000d;
        var centerZ = GetNumber(parameters, "centerZ", 0) / 1000d;
        var radius = GetNumber(parameters, "radius", 0) / 1000d;

        var circle = TryInvoke(sketchManager, "CreateCircle", centerX, centerY, centerZ, radius)
            ?? TryInvoke(sketchManager, "CreateCircle", centerX, centerY, centerZ, centerX + radius, centerY, centerZ)
            ?? TryInvoke(sketchManager, "CreateCircle2", centerX, centerY, centerZ, radius)
            ?? TryInvoke(sketchManager, "CreateCircleByRadius", centerX, centerY, centerZ, radius);

        return circle is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "Failed to create circle",
            }
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["circleId"] = $"circle_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            };
    }

    public object AddRectangle(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "No active model",
            };
        }

        var sketchManager = InvokeProperty(currentModel, "SketchManager");
        if (sketchManager is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "SketchManager unavailable",
            };
        }

        var x1 = GetNumber(parameters, "x1", 0);
        var y1 = GetNumber(parameters, "y1", 0);
        var x2 = GetNumber(parameters, "x2", 100);
        var y2 = GetNumber(parameters, "y2", 0);

        var rectangle = TryInvoke(sketchManager, "CreateCornerRectangle", x1 / 1000d, y1 / 1000d, 0d, x2 / 1000d, y2 / 1000d, 0d)
            ?? TryInvoke(sketchManager, "CreateCenterRectangle", ((x1 + x2) / 2d) / 1000d, ((y1 + y2) / 2d) / 1000d, 0d, Math.Abs(x2 - x1) / 1000d, Math.Abs(y2 - y1) / 1000d);

        return rectangle is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "Failed to create rectangle",
            }
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["rectangleId"] = $"rect_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            };
    }

    public object ExitSketch(bool rebuild)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "No active model",
            };
        }

        var sketchManager = InvokeProperty(currentModel, "SketchManager");
        if (sketchManager is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "SketchManager unavailable",
            };
        }

        var activeSketchBeforeExit = GetProperty(sketchManager, "ActiveSketch") ?? TryInvoke(sketchManager, "GetActiveSketch2");
        var exited = TryInvoke(sketchManager, "InsertSketch", true);
        var activeSketchAfterExit = GetProperty(sketchManager, "ActiveSketch") ?? TryInvoke(sketchManager, "GetActiveSketch2");
        var exitSucceeded = exited is bool boolResult
            ? boolResult
            : activeSketchBeforeExit is null || activeSketchAfterExit is null;
        if (!exitSucceeded)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "Failed to exit sketch",
            };
        }

        if (rebuild)
        {
            _ = TryInvoke(currentModel, "EditRebuild3") ?? TryInvoke(currentModel, "EditRebuild") ?? TryInvoke(currentModel, "ForceRebuild3", false);
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["message"] = "Exited sketch edit mode",
        };
    }

    public object RebuildModel(bool force)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "No model open",
            };
        }

        var attempted = new List<string>();
        var success = false;

        if (force)
        {
            success = TryInvokeCommand(currentModel, attempted, "ForceRebuild3", false)
                || TryInvokeCommand(currentModel, attempted, "ForceRebuild")
                || TryInvokeCommand(currentModel, attempted, "EditRebuild3")
                || TryInvokeCommand(currentModel, attempted, "EditRebuild")
                || TryInvokeCommand(currentModel, attempted, "Rebuild", 1);
        }
        else
        {
            success = TryInvokeCommand(currentModel, attempted, "EditRebuild3")
                || TryInvokeCommand(currentModel, attempted, "EditRebuild")
                || TryInvokeCommand(currentModel, attempted, "Rebuild", 1)
                || TryInvokeCommand(currentModel, attempted, "ForceRebuild3", false);
        }

        return success
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["message"] = "Model rebuilt successfully",
            }
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "Rebuild failed",
                ["attemptedMethods"] = attempted.Cast<object?>().ToList(),
            };
    }

    public SolidWorksFeature CreateExtrude(double depth, double draft = 0, bool reverse = false)
    {
        _ = draft;
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var featureManager = InvokeProperty(currentModel, "FeatureManager") ?? throw new InvalidOperationException("Cannot access FeatureManager");
        var existingFeatures = CaptureRecentFeatureSignatures(currentModel);
        TryClearSelection();
        var selectionRecovery = EnsureSketchSelectionForExtrusion(currentModel);

        var depthInMeters = depth / 1000d;
        object? feature = null;
        List<string> attemptedExtrusionMethods = [];
        List<string> invocationErrors = [];
        var preconditions = DescribeExtrusionPreconditions(currentModel, depthInMeters, reverse, selectionRecovery);

        feature = TryInvokeWithDiagnostics(
                featureManager,
                attemptedExtrusionMethods,
                invocationErrors,
                "FeatureExtrusion",
                true,
                reverse,
                false,
                0,
                0,
                depthInMeters,
                0,
                false,
                false,
                false,
                false,
                0,
                0)
            ?? TryInvokeWithDiagnostics(
                featureManager,
                attemptedExtrusionMethods,
                invocationErrors,
                "FeatureExtrusion3",
                true,
                reverse,
                false,
                0,
                0,
                depthInMeters,
                0,
                false,
                false,
                false,
                false,
                0,
                0,
                false,
                false,
                false,
                false,
                true,
                false,
                true,
                0,
                0,
                false);

        if (feature is null)
        {
            ExecuteExtrusionViaMacro(depthInMeters, reverse);
        }

        var recentFeatures = EnumerateRecentFeatures(currentModel);
        var resolvedFeature = ResolveCreatedExtrusionFeature(currentModel, feature, existingFeatures, recentFeatures);
        if (resolvedFeature is null)
        {
            throw new InvalidOperationException($"Failed to resolve created extrusion feature. Preconditions: {preconditions}. Attempted methods: {string.Join(", ", attemptedExtrusionMethods)}. Invocation errors: {DescribeInvocationErrors(invocationErrors)}. Candidates after extrusion: {DescribeFeatures(recentFeatures)}. Direct return: {DescribeFeature(feature)}");
        }

        var featureName = GetFeatureName(resolvedFeature) ?? "Boss-Extrude1";
        TryClearSelection();
        _ = RebuildModel(force: false);

        return new SolidWorksFeature
        {
            Name = featureName,
            Type = "Extrusion",
            Suppressed = false,
        };
    }

    public double GetDimension(string name)
    {
        EnsureCurrentModel();
        var dimension = ResolveDimension(name) ?? throw new InvalidOperationException($"Dimension \"{name}\" not found. Try format like \"D1@Sketch1\" or \"D1@Boss-Extrude1\"");
        var value = GetNumberFromObject(dimension, "SystemValue", double.NaN);
        if (!double.IsNaN(value))
        {
            return value * 1000d;
        }

        value = GetNumberFromObject(dimension, "Value", double.NaN);
        if (!double.IsNaN(value))
        {
            return value * 1000d;
        }

        value = GetNumberFromMethod(dimension, "GetSystemValue", double.NaN);
        if (!double.IsNaN(value))
        {
            return value * 1000d;
        }

        throw new InvalidOperationException($"Cannot read value of dimension \"{name}\"");
    }

    public void SetDimension(string name, double value)
    {
        EnsureCurrentModel();
        var dimension = ResolveDimension(name) ?? throw new InvalidOperationException($"Dimension \"{name}\" not found. Try format like \"D1@Sketch1\" or \"D1@Boss-Extrude1\"");
        var newValue = value / 1000d;
        var success = false;

        try
        {
            if (HasProperty(dimension, "SystemValue"))
            {
                SetProperty(dimension, "SystemValue", newValue);
                success = true;
            }
            else if (HasProperty(dimension, "Value"))
            {
                SetProperty(dimension, "Value", newValue);
                success = true;
            }
            else if (TryInvoke(dimension, "SetSystemValue", newValue) is bool systemValueSet)
            {
                success = systemValueSet;
            }
            else if (TryInvoke(dimension, "SetValue", newValue) is bool valueSet)
            {
                success = valueSet;
            }
        }
        catch
        {
            success = false;
        }

        if (!success)
        {
            var equationManager = TryInvoke(currentModel!, "GetEquationMgr");
            if (equationManager is not null)
            {
                var count = Convert.ToInt32(GetMethodValue(equationManager, "GetCount") ?? 0);
                for (var i = 0; i < count; i++)
                {
                    var equation = Convert.ToString(GetEquationAt(equationManager, i));
                    if (!string.IsNullOrWhiteSpace(equation) && equation.Contains(name, StringComparison.OrdinalIgnoreCase))
                    {
                        SetEquationAt(equationManager, i, $"\"{name}\" = {value}");
                        success = true;
                        break;
                    }
                }
            }
        }

        TryClearSelection();
        if (!success)
        {
            throw new InvalidOperationException($"Failed to set dimension \"{name}\" to {value}mm");
        }

        TryInvoke(currentModel!, "EditRebuild3");
    }

    public void ExportFile(string filePath, string format)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var resolvedFormat = NormalizeExportFormat(format, filePath);
        _ = EnsureModelPathForInterop(currentModel, "export-source-model");

        var documentType = GetSolidWorksDocumentType(currentModel);
        if (resolvedFormat == "pdf" && documentType != 3)
        {
            throw new InvalidOperationException("PDF export is only supported for drawing documents.");
        }

        if (!TrySaveDocument(currentModel, filePath, 2))
        {
            throw new InvalidOperationException($"Failed to export to {resolvedFormat.ToUpperInvariant()}: Export returned false");
        }
    }

    public object RunMacro(string macroPath, string moduleName, string procedureName, object[]? args = null)
    {
        EnsureConnected();
        _ = args;
        return TryInvoke(swApp!, "RunMacro2", macroPath, moduleName, procedureName, 1, 0) ?? throw new InvalidOperationException("Failed to run macro");
    }

    public MassProperties GetMassProperties()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var docType = Convert.ToInt32(GetMethodValue(currentModel, "GetType") ?? 0);
        if (docType is not (1 or 2))
        {
            throw new InvalidOperationException("Mass properties only available for parts and assemblies");
        }

        object? massProps = null;
        var extension = InvokeProperty(currentModel, "Extension");
        if (extension is not null)
        {
            massProps = TryInvoke(extension, "CreateMassProperty") ?? TryInvoke(extension, "CreateMassProperty2");
        }

        massProps ??= TryInvoke(currentModel, "GetMassProperties");
        if (massProps is null)
        {
            throw new InvalidOperationException("Failed to create mass property object");
        }

        if (TryInvoke(massProps, "Update") is null)
        {
            _ = TryInvoke(massProps, "Recalculate");
        }

        var mass = GetNumberFromObject(massProps, "Mass", 0);
        var volume = GetNumberFromObject(massProps, "Volume", 0);
        var surfaceArea = GetNumberFromObject(massProps, "SurfaceArea", 0);
        var density = GetNumberFromObject(massProps, "Density", 0);
        var center = GetArrayFromObject(massProps, "CenterOfMass");
        var moi = GetArrayFromObject(massProps, "MomentOfInertia");

        return new MassProperties
        {
            Mass = mass,
            Volume = volume,
            SurfaceArea = surfaceArea,
            CenterOfMass = new CenterOfMass
            {
                X = center.Length >= 3 ? center[0] * 1000d : 0,
                Y = center.Length >= 3 ? center[1] * 1000d : 0,
                Z = center.Length >= 3 ? center[2] * 1000d : 0,
            },
        };
    }

    public object? GetCurrentModel()
    {
        EnsureCurrentModel();
        return currentModel;
    }

    public Dictionary<string, object?> GetActiveDocumentInfo()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["connected"] = IsConnected(),
                ["hasActiveDocument"] = false,
            };
        }

        var path = GetString(currentModel, "GetPathName") ?? string.Empty;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["connected"] = IsConnected(),
            ["hasActiveDocument"] = true,
            ["title"] = GetString(currentModel, "GetTitle") ?? "Unknown",
            ["path"] = path,
            ["type"] = GetSolidWorksDocumentType(currentModel) switch
            {
                1 => "Part",
                2 => "Assembly",
                3 => "Drawing",
                _ => "Unknown",
            },
            ["isSaved"] = !string.IsNullOrWhiteSpace(path),
        };
    }

    public string GetCurrentModelTitleOrPath()
    {
        EnsureCurrentModel();
        return GetString(currentModel, "GetTitle") ?? GetString(currentModel, "GetPathName") ?? "Unknown";
    }

    public string? GetCurrentModelPath()
    {
        EnsureCurrentModel();
        return GetString(currentModel, "GetPathName");
    }

    public string SaveDocument(string filePath)
    {
        EnsureCurrentModel();
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var normalizedPath = NormalizeManagedOutputPath(filePath);
        EnsureParentDirectory(normalizedPath);

        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        TryClearSelection();
        if (!TrySaveDocument(currentModel, normalizedPath, 1))
        {
            throw new InvalidOperationException($"Failed to save document: {normalizedPath}");
        }

        if (GetSolidWorksDocumentType(currentModel) is 1 or 2)
        {
            lastDrawingSourceModelPath = normalizedPath;
        }

        return normalizedPath;
    }

    public string SaveActiveDocument()
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var currentPath = GetString(currentModel, "GetPathName");
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            throw new InvalidOperationException("The active document has no saved path; use save_document first.");
        }

        TryClearSelection();
        var saved = TryInvoke(currentModel, "Save3", 1, 0, 0);
        if (saved is not bool success || !success)
        {
            saved = TryInvoke(currentModel, "Save");
        }

        if (saved is not bool didSave || !didSave)
        {
            throw new InvalidOperationException($"Failed to save active document: {currentPath}");
        }

        if (GetSolidWorksDocumentType(currentModel) is 1 or 2)
        {
            lastDrawingSourceModelPath = currentPath;
        }

        return currentPath;
    }

    public object CreateDrawingFromCurrentModel(string? templatePath)
    {
        EnsureConnected();
        EnsureCurrentModel();
        if (currentModel is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "No model open to create drawing from",
            };
        }

        var sourceModel = currentModel;
        var sourceModelPath = EnsureModelPathForInterop(sourceModel, "drawing-source-model");
        var resolvedTemplatePath = ResolveDrawingTemplatePath(templatePath);
        var (drawing, error) = TryCreateDocumentFromTemplate(swApp!, resolvedTemplatePath ?? string.Empty);
        if (drawing is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = $"Cannot create drawing with template: {resolvedTemplatePath ?? templatePath ?? string.Empty}",
                ["details"] = error,
            };
        }

        currentModel = drawing;
        lastDrawingSourceModelPath = sourceModelPath;

        List<string> warnings = [];
        TryAddStandardViews(drawing, sourceModelPath, warnings);

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["drawingName"] = GetString(drawing, "GetTitle") ?? "Drawing",
            ["templatePath"] = resolvedTemplatePath ?? "<SolidWorks default>",
            ["sourceModelPath"] = sourceModelPath,
            ["warnings"] = warnings.Cast<object?>().ToList(),
            ["message"] = warnings.Count == 0
                ? "Created new drawing from current model"
                : $"Created new drawing from current model with warnings: {string.Join("; ", warnings)}",
        };
    }

    public IReadOnlyList<Dictionary<string, object?>> ListDrawingViews()
    {
        EnsureCurrentModel();
        if (currentModel is null || GetSolidWorksDocumentType(currentModel) != 3)
        {
            throw new InvalidOperationException("Current document must be a drawing");
        }

        var views = new List<Dictionary<string, object?>>();
        var currentView = TryInvoke(currentModel, "GetFirstView");
        var index = 0;
        while (currentView is not null && index < 500)
        {
            views.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["index"] = index,
                ["name"] = GetString(currentView, "Name") ?? GetString(currentView, "GetName2") ?? $"View{index}",
                ["type"] = Convert.ToInt32(GetProperty(currentView, "Type") ?? 0),
                ["scale"] = GetProperty(currentView, "ScaleDecimal"),
            });
            currentView = TryInvoke(currentView, "GetNextView");
            index++;
        }

        return views;
    }

    public IReadOnlyList<string> ListSheets()
    {
        EnsureCurrentModel();
        if (currentModel is null || GetSolidWorksDocumentType(currentModel) != 3)
        {
            throw new InvalidOperationException("Current document must be a drawing");
        }

        return AsEnumerable(TryInvoke(currentModel, "GetSheetNames"))
            .Select(item => Convert.ToString(item) ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
    }

    public Dictionary<string, object?> ActivateSheet(string sheetName)
    {
        EnsureCurrentModel();
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
        if (currentModel is null || GetSolidWorksDocumentType(currentModel) != 3)
        {
            throw new InvalidOperationException("Current document must be a drawing");
        }

        var activated = TryInvoke(currentModel, "ActivateSheet", sheetName) is bool success && success;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = activated,
            ["sheet"] = sheetName,
            ["message"] = activated ? $"Activated sheet '{sheetName}'." : $"Could not activate sheet '{sheetName}'.",
        };
    }

    public Dictionary<string, object?> AddSheet(string name, string paperSize)
    {
        EnsureCurrentModel();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (currentModel is null || GetSolidWorksDocumentType(currentModel) != 3)
        {
            throw new InvalidOperationException("Current document must be a drawing");
        }

        var paper = paperSize.Trim().ToUpperInvariant() switch
        {
            "A4" => 6,
            "A3" => 8,
            "A2" => 9,
            "A1" => 10,
            "A0" => 11,
            _ => 6,
        };

        var created = TryInvoke(currentModel, "NewSheet3", name, paper, 1d, 1d, false, string.Empty, 0d, 0d, string.Empty, false)
            ?? TryInvoke(currentModel, "SetupSheet5", name, paper, 1d, 1d, false, string.Empty, 0d, 0d, string.Empty, false, 0d, 0d, 0d, 0d, false);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = created is not null,
            ["sheet"] = name,
            ["sheets"] = ListSheets(),
            ["message"] = created is not null ? $"Added sheet '{name}'." : $"SOLIDWORKS did not add sheet '{name}'.",
        };
    }

    public Dictionary<string, object?> ActivateDrawingView(string viewName)
    {
        EnsureCurrentModel();
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
        if (currentModel is null || GetSolidWorksDocumentType(currentModel) != 3)
        {
            throw new InvalidOperationException("Current document must be a drawing");
        }

        var activated = TryInvoke(currentModel, "ActivateView", viewName) is bool success && success;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = activated,
            ["view"] = viewName,
            ["message"] = activated ? $"Activated drawing view '{viewName}'." : $"Could not activate drawing view '{viewName}'.",
        };
    }

    public Dictionary<string, object?> SetDrawingView(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        if (currentModel is null || GetSolidWorksDocumentType(currentModel) != 3)
        {
            throw new InvalidOperationException("Current document must be a drawing");
        }

        var viewName = GetString(parameters, "viewName") ?? throw new InvalidOperationException("viewName is required.");
        _ = ActivateDrawingView(viewName);
        var currentView = ListDrawingViews().FirstOrDefault(view => string.Equals(Convert.ToString(view["name"]), viewName, StringComparison.OrdinalIgnoreCase));
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = currentView is not null,
            ["view"] = viewName,
            ["scale"] = currentView is not null && currentView.TryGetValue("scale", out var scale) ? scale : null,
            ["message"] = currentView is not null ? $"Activated drawing view '{viewName}'." : $"Drawing view '{viewName}' was not found.",
        };
    }

    public object AddDrawingView(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        if (currentModel is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "Current document must be a drawing",
            };
        }

        var modelPath = GetResolvedDrawingModelPath(parameters);
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "Model path is unavailable. Save the source model or create the drawing from a model first.",
            };
        }

        Dictionary<string, string> orientationMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["front"] = "*Front",
            ["top"] = "*Top",
            ["right"] = "*Right",
            ["back"] = "*Back",
            ["bottom"] = "*Bottom",
            ["left"] = "*Left",
            ["iso"] = "*Isometric",
            ["current"] = "*Current",
        };

        var viewType = parameters.TryGetValue("viewType", out var viewTypeValue) && !string.IsNullOrWhiteSpace(Convert.ToString(viewTypeValue))
            ? Convert.ToString(viewTypeValue)!
            : "front";
        var viewName = orientationMap.TryGetValue(viewType, out var mapped) ? mapped : "*Front";
        var view = TryInvoke(currentModel, "CreateDrawViewFromModelView3", modelPath, viewName, GetNumber(parameters, "x", 0) / 1000d, GetNumber(parameters, "y", 0) / 1000d, 0d);
        if (view is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = false,
                ["error"] = "Failed to create view",
                ["modelPath"] = modelPath,
                ["viewType"] = viewType,
            };
        }

        var scale = GetNumber(parameters, "scale", 1);
        if (scale > 0)
        {
            SetProperty(view, "ScaleDecimal", scale);
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["viewType"] = viewType,
            ["modelPath"] = modelPath,
            ["message"] = $"Added {viewType} view at ({GetNumber(parameters, "x", 0)}, {GetNumber(parameters, "y", 0)})",
        };
    }

    public object? GetApp() => swApp;

    [SupportedOSPlatform("windows")]
    private static object CreateSolidWorksApplication()
    {
        var type = Type.GetTypeFromProgID("SldWorks.Application") ?? throw new InvalidOperationException("SolidWorks COM ProgID not found");

        var activeInstance = TryGetActiveSolidWorksInstance(type);
        if (activeInstance is not null)
        {
            return activeInstance;
        }

        return Activator.CreateInstance(type) ?? throw new InvalidOperationException("Unable to create SolidWorks application instance");
    }

    [SupportedOSPlatform("windows")]
    private static object? TryGetActiveSolidWorksInstance(Type solidWorksType)
    {
        try
        {
            var clsid = solidWorksType.GUID;
            return GetActiveObject(ref clsid, IntPtr.Zero, out var activeInstance) == 0 ? activeInstance : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static void SetVisible(object app, bool visible)
    {
        SetProperty(app, "Visible", visible);
    }

    private static bool TryBringApplicationToFront(object app)
    {
        try
        {
            var handleValue = TryInvoke(app, "GetHWnd");
            if (!TryGetWindowHandle(handleValue, out var windowHandle))
            {
                var frame = TryInvoke(app, "Frame");
                handleValue = frame is null ? null : TryInvoke(frame, "GetHWnd");
                if (!TryGetWindowHandle(handleValue, out windowHandle))
                {
                    return TryBringSolidWorksProcessToFront();
                }
            }

            if (ForceForegroundWindow(windowHandle))
            {
                return true;
            }

            return TryBringSolidWorksProcessToFront();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBringSolidWorksProcessToFront()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("SLDWORKS"))
            {
                if (process.MainWindowHandle != IntPtr.Zero && ForceForegroundWindow(process.MainWindowHandle))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool ForceForegroundWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _ = ShowWindow(windowHandle, SwRestore);
        _ = BringWindowToTop(windowHandle);

        if (SetForegroundWindow(windowHandle))
        {
            return true;
        }

        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        var currentThreadId = GetCurrentThreadId();
        var targetThreadId = GetWindowThreadProcessId(windowHandle, out _);
        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);

        var attachedToTarget = false;
        var attachedToForeground = false;

        try
        {
            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                attachedToTarget = AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attachedToForeground = AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            _ = ShowWindow(windowHandle, SwRestore);
            _ = BringWindowToTop(windowHandle);
            return SetForegroundWindow(windowHandle);
        }
        finally
        {
            if (attachedToForeground)
            {
                _ = AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }

            if (attachedToTarget)
            {
                _ = AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }

    private static bool TryGetWindowHandle(object? handleValue, out IntPtr windowHandle)
    {
        if (handleValue is nint nativeInt && nativeInt != 0)
        {
            windowHandle = nativeInt;
            return true;
        }

        if (handleValue is int intHandle && intHandle != 0)
        {
            windowHandle = new IntPtr(intHandle);
            return true;
        }

        if (handleValue is long longHandle && longHandle != 0)
        {
            windowHandle = new IntPtr(longHandle);
            return true;
        }

        windowHandle = IntPtr.Zero;
        return false;
    }

    private static object? TryCreatePartFromTemplate(object swApplication, out List<string> inspectedLocations, out string? templatePathUsed)
    {
        ArgumentNullException.ThrowIfNull(swApplication);

        templatePathUsed = null;
        inspectedLocations = new List<string>();
        var templateCandidates = DiscoverPartTemplateCandidates(swApplication, inspectedLocations);

        foreach (var templatePath in templateCandidates)
        {
            var expandedTemplatePath = Environment.ExpandEnvironmentVariables(templatePath);

            if (string.IsNullOrWhiteSpace(expandedTemplatePath))
            {
                continue;
            }

            var (created, error) = TryCreateDocumentFromTemplate(swApplication, expandedTemplatePath);
            if (created is not null)
            {
                templatePathUsed = expandedTemplatePath;
                return created;
            }

            var existsState = File.Exists(expandedTemplatePath) ? "exists" : "missing";
            var errorSuffix = string.IsNullOrWhiteSpace(error) ? string.Empty : $" (error: {error})";
            inspectedLocations.Add($"<template failed to open ({existsState})> {expandedTemplatePath}{errorSuffix}");
        }

        inspectedLocations.Add("<SolidWorks default template via NewDocument(\"\")>");
        var (fallback, fallbackError) = TryCreateDocumentFromTemplate(swApplication, string.Empty);
        if (fallback is not null)
        {
            templatePathUsed = "<SolidWorks default template via NewDocument(\"\")>";
        }
        else if (!string.IsNullOrWhiteSpace(fallbackError))
        {
            inspectedLocations.Add($"<SolidWorks default template failed> {fallbackError}");
        }

        return fallback;
    }

    private static List<string> DiscoverPartTemplateCandidates(object swApplication, List<string> inspectedLocations)
    {
        return DiscoverTemplateCandidates(
            swApplication,
            inspectedLocations,
            "SOLIDWORKS_PART_TEMPLATE",
            TryGetPartDocumentTemplate,
            [13, 14],
            ".prtdot");
    }

    private string? ResolveDrawingTemplatePath(string? templatePath)
    {
        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            return templatePath;
        }

        if (swApp is null)
        {
            return null;
        }

        List<string> inspectedLocations = [];
        return DiscoverDrawingTemplateCandidates(swApp, inspectedLocations).FirstOrDefault();
    }

    private static List<string> DiscoverDrawingTemplateCandidates(object swApplication, List<string> inspectedLocations)
    {
        return DiscoverTemplateCandidates(
            swApplication,
            inspectedLocations,
            "SOLIDWORKS_DRAWING_TEMPLATE",
            TryGetDrawingDocumentTemplate,
            [15],
            ".drwdot");
    }

    private static List<string> DiscoverTemplateCandidates(object swApplication, List<string> inspectedLocations, string environmentVariableName, Func<object, string?> preferredTemplateResolver, IReadOnlyList<int> preferenceIds, string extension)
    {
        List<string> templateCandidates = [];

        var envTemplate = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(envTemplate))
        {
            templateCandidates.Add(envTemplate);
            inspectedLocations.Add(envTemplate);
        }

        var preferredTemplate = preferredTemplateResolver(swApplication);
        if (!string.IsNullOrWhiteSpace(preferredTemplate))
        {
            templateCandidates.Add(preferredTemplate);
            inspectedLocations.Add($"<SolidWorks preferred template> {preferredTemplate}");
        }

        foreach (var preferenceId in preferenceIds)
        {
            var preferenceTemplate = Convert.ToString(TryInvoke(swApplication, "GetUserPreferenceStringValue", preferenceId));
            if (!string.IsNullOrWhiteSpace(preferenceTemplate))
            {
                templateCandidates.Add(preferenceTemplate);
                inspectedLocations.Add(preferenceTemplate);
            }
        }

        foreach (var root in GetCommonTemplateRoots())
        {
            AddTemplatesFromPath(root, templateCandidates, inspectedLocations, extension);
        }

        foreach (var root in GetTemplateRootsFromRegistry(inspectedLocations))
        {
            AddTemplatesFromPath(root, templateCandidates, inspectedLocations, extension);
        }

        return templateCandidates
            .Select(path => path?.Trim().Trim('"'))
            .Where(path => !string.IsNullOrWhiteSpace(path) && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> GetCommonTemplateRoots()
    {
        var versionCandidates = Enumerable.Range(DateTime.UtcNow.Year - 8, 10)
            .Select(year => $"SOLIDWORKS {year}")
            .Reverse()
            .ToArray();

        var roots = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOLIDWORKS", "templates"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOLIDWORKS", "lang", "english", "tutorial", "templates"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "SOLIDWORKS", "templates"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "SOLIDWORKS", "lang", "english", "tutorial", "templates"),
        };

        foreach (var version in versionCandidates)
        {
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "SOLIDWORKS", version, "templates"));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOLIDWORKS", version, "templates"));
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> GetTemplateRootsFromRegistry(List<string> inspectedLocations)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var discovered = new List<string>();

        using var solidWorksKey = Registry.CurrentUser.OpenSubKey(@"Software\SolidWorks");
        if (solidWorksKey is null)
        {
            return discovered;
        }

        foreach (var versionKeyName in solidWorksKey.GetSubKeyNames())
        {
            if (!versionKeyName.StartsWith("SOLIDWORKS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var versionKey = solidWorksKey.OpenSubKey(versionKeyName);
            if (versionKey is null)
            {
                continue;
            }

            CollectTemplateValuesFromRegistry(versionKey, $"HKCU\\Software\\SolidWorks\\{versionKeyName}", discovered, inspectedLocations, maxDepth: 4);
        }

        return discovered.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void CollectTemplateValuesFromRegistry(RegistryKey key, string keyPath, List<string> discovered, List<string> inspectedLocations, int maxDepth)
    {
        foreach (var valueName in key.GetValueNames())
        {
            var raw = key.GetValue(valueName);
            foreach (var token in ExpandRegistryValueToPaths(raw))
            {
                var hasTemplateHint = valueName.IndexOf("template", StringComparison.OrdinalIgnoreCase) >= 0
                    || token.IndexOf("template", StringComparison.OrdinalIgnoreCase) >= 0
                    || token.EndsWith(".prtdot", StringComparison.OrdinalIgnoreCase);

                if (!hasTemplateHint)
                {
                    continue;
                }

                inspectedLocations.Add($"{keyPath}::{valueName}={token}");
                discovered.Add(token);
            }
        }

        if (maxDepth <= 0)
        {
            return;
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subKey = key.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                continue;
            }

            CollectTemplateValuesFromRegistry(subKey, $"{keyPath}\\{subKeyName}", discovered, inspectedLocations, maxDepth - 1);
        }
    }

    private static IEnumerable<string> ExpandRegistryValueToPaths(object? registryValue)
    {
        if (registryValue is null)
        {
            return [];
        }

        if (registryValue is string single)
        {
            return SplitPathTokens(Environment.ExpandEnvironmentVariables(single));
        }

        if (registryValue is string[] multi)
        {
            var results = new List<string>();
            foreach (var value in multi)
            {
                results.AddRange(SplitPathTokens(Environment.ExpandEnvironmentVariables(value)));
            }

            return results;
        }

        return [];
    }

    private static IEnumerable<string> SplitPathTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if ((value.StartsWith("\\\\", StringComparison.Ordinal) || value.Contains(":\\", StringComparison.Ordinal))
            && (value.EndsWith(".prtdot", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".asmdot", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".drwdot", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".slddrt", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".sldbomtbt", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".sldwldtbt", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".sldtbt", StringComparison.OrdinalIgnoreCase)))
        {
            return [value];
        }

        return value
            .Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token));
    }

    private static string? TryGetPartDocumentTemplate(object swApplication)
    {
        var attempts = new object?[]
        {
            TryInvoke(swApplication, "GetDocumentTemplate", 1, 0, 0d, 0d),
            TryInvoke(swApplication, "GetDocumentTemplate", 1, string.Empty, 0, 0d, 0d),
            TryInvoke(swApplication, "GetDocumentTemplate", 1, string.Empty, 0, 0, 0),
            TryInvoke(swApplication, "GetTemplatePathName", 1, 0, 0d, 0d),
            TryInvoke(swApplication, "GetTemplatePathName", 1),
        };

        foreach (var attempt in attempts)
        {
            var candidate = Convert.ToString(attempt);
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.EndsWith(".prtdot", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? TryGetAssemblyDocumentTemplate(object swApplication)
    {
        var attempts = new object?[]
        {
            TryInvoke(swApplication, "GetDocumentTemplate", 2, 0, 0d, 0d),
            TryInvoke(swApplication, "GetDocumentTemplate", 2, string.Empty, 0, 0d, 0d),
            TryInvoke(swApplication, "GetDocumentTemplate", 2, string.Empty, 0, 0, 0),
            TryInvoke(swApplication, "GetTemplatePathName", 2, 0, 0d, 0d),
            TryInvoke(swApplication, "GetTemplatePathName", 2),
        };

        foreach (var attempt in attempts)
        {
            var candidate = Convert.ToString(attempt);
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.EndsWith(".asmdot", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? TryGetDrawingDocumentTemplate(object swApplication)
    {
        var attempts = new object?[]
        {
            TryInvoke(swApplication, "GetDocumentTemplate", 3, 0, 0d, 0d),
            TryInvoke(swApplication, "GetDocumentTemplate", 3, string.Empty, 0, 0d, 0d),
            TryInvoke(swApplication, "GetDocumentTemplate", 3, string.Empty, 0, 0, 0),
            TryInvoke(swApplication, "GetTemplatePathName", 3, 0, 0d, 0d),
            TryInvoke(swApplication, "GetTemplatePathName", 3),
        };

        foreach (var attempt in attempts)
        {
            var candidate = Convert.ToString(attempt);
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.EndsWith(".drwdot", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private int ApplySelection(SelectionSpec selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (currentModel is null)
        {
            return 0;
        }

        TryClearSelection();
        var count = 0;

        foreach (var planeName in selection.Planes ?? [])
        {
            var feature = TryInvoke(currentModel, "FeatureByName", planeName);
            if (!TrySelectFeatureLikeObject(feature))
            {
                var extension = GetProperty(currentModel, "Extension");
                var selected = extension is null
                    ? null
                    : TryInvoke(extension, "SelectByID2", planeName, "PLANE", 0d, 0d, 0d, count > 0, 0, null, 0);
                if (selected is not bool success || !success)
                {
                    throw new InvalidOperationException($"Failed to select plane: {planeName}");
                }
            }

            count++;
        }

        foreach (var axisName in selection.Axes ?? [])
        {
            var feature = TryInvoke(currentModel, "FeatureByName", axisName);
            if (!TrySelectFeatureLikeObject(feature))
            {
                var extension = GetProperty(currentModel, "Extension");
                var selected = extension is null
                    ? null
                    : TryInvoke(extension, "SelectByID2", axisName, "AXIS", 0d, 0d, 0d, count > 0, 0, null, 0);
                if (selected is not bool success || !success)
                {
                    throw new InvalidOperationException($"Failed to select axis: {axisName}");
                }
            }

            count++;
        }

        foreach (var sketchName in selection.Sketches ?? [])
        {
            var feature = TryInvoke(currentModel, "FeatureByName", sketchName);
            if (!TrySelectFeatureLikeObject(feature))
            {
                var extension = GetProperty(currentModel, "Extension");
                var selected = extension is null
                    ? null
                    : TryInvoke(extension, "SelectByID2", sketchName, "SKETCH", 0d, 0d, 0d, count > 0, 0, null, 0);
                if (selected is not bool success || !success)
                {
                    throw new InvalidOperationException($"Failed to select sketch: {sketchName}");
                }
            }

            count++;
        }

        foreach (var featureName in selection.Features ?? [])
        {
            var feature = TryInvoke(currentModel, "FeatureByName", featureName);
            if (!TrySelectFeatureLikeObject(feature))
            {
                throw new InvalidOperationException($"Failed to select feature: {featureName}");
            }

            count++;
        }

        foreach (var componentName in selection.Components ?? [])
        {
            var component = ResolveAssemblyComponent(componentName) ?? throw new InvalidOperationException($"Component not found: {componentName}");
            if (!TrySelectEntity(component, count > 0))
            {
                throw new InvalidOperationException($"Failed to select component: {componentName}");
            }

            count++;
        }

        return count;
    }

    private Dictionary<string, object?> ExtractTransformOrigin(object? transform)
    {
        if (transform is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["x"] = 0d,
                ["y"] = 0d,
                ["z"] = 0d,
            };
        }

        var array = GetArrayFromObject(transform, "ArrayData");
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = array.Length > 9 ? array[9] * 1000d : 0d,
            ["y"] = array.Length > 10 ? array[10] * 1000d : 0d,
            ["z"] = array.Length > 11 ? array[11] * 1000d : 0d,
        };
    }

    private object? ResolveAssemblyComponent(string componentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        if (currentModel is null || !IsAssemblyDocument(currentModel))
        {
            return null;
        }

        return ListAssemblyComponentsCore(includeHidden: true)
            .FirstOrDefault(component => string.Equals(GetString(component, "Name2"), componentName, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<object> ListAssemblyComponentsCore(bool includeHidden)
    {
        if (currentModel is null)
        {
            return [];
        }

        var raw = TryInvoke(currentModel, "GetComponents", includeHidden);
        return AsEnumerable(raw);
    }

    private string GetResolvedDrawingModelPath(Dictionary<string, object?> parameters)
    {
        if (parameters.TryGetValue("modelPath", out var explicitPathValue) && !string.IsNullOrWhiteSpace(Convert.ToString(explicitPathValue)))
        {
            return Convert.ToString(explicitPathValue)!;
        }

        if (!string.IsNullOrWhiteSpace(lastDrawingSourceModelPath))
        {
            return lastDrawingSourceModelPath;
        }

        return GetCurrentModelPath() ?? string.Empty;
    }

    private string EnsureModelPathForInterop(object model, string fallbackName)
    {
        var modelPath = GetString(model, "GetPathName");
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            return modelPath;
        }

        var directory = Path.Combine(Path.GetTempPath(), "solidworks-mcp-models");
        Directory.CreateDirectory(directory);

        var documentType = GetSolidWorksDocumentType(model);
        var extension = documentType switch
        {
            2 => ".sldasm",
            3 => ".slddrw",
            _ => ".sldprt",
        };

        var title = GetString(model, "GetTitle") ?? fallbackName;
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeTitle = new string(title.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        var tempPath = Path.Combine(directory, $"{safeTitle}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{extension}");
        if (!TrySaveDocument(model, tempPath, 1))
        {
            throw new InvalidOperationException($"Model must be saved before this operation can continue. Temporary save failed for {safeTitle}.");
        }

        return tempPath;
    }

    private static void TryAddStandardViews(object drawing, string modelPath, List<string> warnings)
    {
        try
        {
            var firstView = TryInvoke(drawing, "CreateDrawViewFromModelView3", modelPath, "*Front", 0.15d, 0.15d, 0d);
            if (firstView is null)
            {
                warnings.Add("Failed to create front view - drawing is empty");
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"View creation error: {ex.Message}");
        }
    }

    private static (object? Document, string? Error) TryCreateDocumentFromTemplate(object swApplication, string templatePath)
    {
        var errors = 0;
        var warnings = 0;

        try
        {
            var attemptErrors = new List<string>();
            foreach (var openTemplate in BuildTemplateOpenCandidates(templatePath))
            {
                AppLogger.Debug("Template open attempt started", new { templatePath, openTemplate, exists = File.Exists(openTemplate) });

                if (TryInvoke(swApplication, "NewDocument", openTemplate, 0, 0d, 0d) is { } createdByNewDocument)
                {
                    AppLogger.Info("Template open succeeded via NewDocument", new { openTemplate });
                    return (createdByNewDocument, null);
                }

                if (TryInvoke(swApplication, "INewDocument2", openTemplate, 0, 0d, 0d) is { } createdByINewDocument2)
                {
                    AppLogger.Info("Template open succeeded via INewDocument2", new { openTemplate });
                    return (createdByINewDocument2, null);
                }

                if (TryInvoke(swApplication, "NewDoc6", openTemplate, 0, 0d, 0d, errors, warnings) is { } createdByNewDoc6)
                {
                    AppLogger.Info("Template open succeeded via NewDoc6", new { openTemplate, errors, warnings });
                    return (createdByNewDoc6, null);
                }

                if (TryInvoke(swApplication, "OpenDoc6", openTemplate, 1, 1, string.Empty, errors, warnings) is { } createdByOpenDoc6)
                {
                    AppLogger.Info("Template open succeeded via OpenDoc6", new { openTemplate, errors, warnings });
                    return (createdByOpenDoc6, null);
                }

                var appError = ReadSolidWorksAppError(swApplication);
                attemptErrors.Add($"openTemplate={openTemplate}, exists={File.Exists(openTemplate)}, errors={errors}, warnings={warnings}, appError={appError}");
                AppLogger.Warn("Template open attempt failed", new { openTemplate, errors, warnings, appError });
            }

            var appErrorSummary = ReadSolidWorksAppError(swApplication);
            var details = new List<string>(attemptErrors)
            {
                $"finalAppError={appErrorSummary}",
            };

            return (null, details.Count == 0 ? "SolidWorks returned null document." : string.Join(", ", details));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Template open processing threw exception", ex.Message);
            return (null, ex.Message);
        }
    }

    private static IEnumerable<string> BuildTemplateOpenCandidates(string templatePath)
    {
        var candidates = new List<string>();

        if (string.IsNullOrWhiteSpace(templatePath))
        {
            candidates.Add(string.Empty);
            return candidates;
        }

        var normalized = Environment.ExpandEnvironmentVariables(templatePath).Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            candidates.Add(normalized);
        }

        var resolvedUnc = ResolveUncPath(normalized);
        if (!string.IsNullOrWhiteSpace(resolvedUnc) && !string.Equals(resolvedUnc, normalized, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(resolvedUnc);
            candidates.Add($"\"{resolvedUnc}\"");
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadSolidWorksAppError(object swApplication)
    {
        var details = new List<string>();
        var appType = swApplication.GetType();

        try
        {
            var getLastError = appType.GetMethod("GetLastError");
            var getErrorMsg = appType.GetMethod("GetErrorCodeString") ?? appType.GetMethod("GetErrorString");
            var lastCode = getLastError?.Invoke(swApplication, null);
            var lastText = lastCode is null ? null : getErrorMsg?.Invoke(swApplication, [lastCode]);

            if (lastCode is not null)
            {
                details.Add($"code={lastCode}");
            }

            if (!string.IsNullOrWhiteSpace(Convert.ToString(lastText)))
            {
                details.Add($"message={lastText}");
            }
        }
        catch
        {
            details.Add("errorReader=unavailable");
        }

        try
        {
            var rev = TryInvoke(swApplication, "RevisionNumber");
            if (rev is not null)
            {
                details.Add($"revision={rev}");
            }
        }
        catch
        {
        }

        return details.Count == 0 ? "none" : string.Join("|", details);
    }

    private static string? ResolveUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length < 2 || path[1] != ':')
        {
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var drive = path[..2];
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c net use {drive}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit(2000);

            var unc = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.StartsWith("\\\\", StringComparison.Ordinal));

            if (string.IsNullOrWhiteSpace(unc))
            {
                return null;
            }

            return Path.Combine(unc.TrimEnd('\\'), path[2..].TrimStart('\\'));
        }
        catch
        {
            return null;
        }
    }

    private static void AddTemplatesFromPath(string path, List<string> templateCandidates, List<string> inspectedLocations, string extension)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        inspectedLocations.Add(path);

        if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            templateCandidates.Add(path);
            return;
        }

        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            foreach (var template in Directory.EnumerateFiles(path, $"*{extension}", SearchOption.TopDirectoryOnly))
            {
                templateCandidates.Add(template);
            }
        }
        catch
        {
            // ignore inaccessible template folders
        }
    }

    private static int GetDocumentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".sldasm" => 2,
            ".slddrw" => 3,
            _ => 1,
        };
    }

    private static string NormalizeExportFormat(string? format, string filePath)
    {
        var resolvedFormat = string.IsNullOrWhiteSpace(format)
            ? Path.GetExtension(filePath).TrimStart('.')
            : format;

        resolvedFormat = resolvedFormat.ToLowerInvariant();
        return resolvedFormat switch
        {
            "stp" => "step",
            "igs" => "iges",
            _ when resolvedFormat is "step" or "iges" or "stl" or "pdf" or "dxf" or "dwg" => resolvedFormat,
            _ => throw new InvalidOperationException($"Unsupported export format: {format ?? Path.GetExtension(filePath)}"),
        };
    }

    private static int GetSolidWorksDocumentType(object model)
    {
        var documentTypeValue = GetProperty(model, "GetType") ?? TryInvoke(model, "GetType");
        if (documentTypeValue is not null && int.TryParse(Convert.ToString(documentTypeValue), out var documentType))
        {
            return documentType;
        }

        var path = GetString(model, "GetPathName");
        return string.IsNullOrWhiteSpace(path) ? 1 : GetDocumentType(path);
    }

    private bool TrySaveDocument(object model, string filePath, int options)
    {
        var normalizedPath = NormalizeManagedOutputPath(filePath);
        EnsureParentDirectory(normalizedPath);
        var extension = GetProperty(model, "Extension");
        return TryInvoke(model, "SaveAs3", normalizedPath, 0, options) is true
            || TryInvoke(model, "SaveAs4", normalizedPath, 0, options, 0, 0) is true
            || TryInvoke(extension, "SaveAs", normalizedPath, 0, options, null, 0, 0) is true;
    }

    private void EnsureConnected()
    {
        if (swApp is null)
        {
            throw new InvalidOperationException("Not connected to SolidWorks");
        }
    }

    private void EnsureCurrentModel()
    {
        if (swApp is null)
        {
            return;
        }

        try
        {
            var activeDoc = GetProperty(swApp, "ActiveDoc");
            if (activeDoc is not null)
            {
                if (!ReferenceEquals(currentModel, activeDoc))
                {
                    currentModel = activeDoc;
                }
            }
            else if (currentModel is null)
            {
                var docCount = Convert.ToInt32(GetMethodValue(swApp, "GetDocumentCount") ?? 0);
                if (docCount > 0)
                {
                    var docs = GetMethodValue(swApp, "GetDocuments");
                    if (docs is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var doc in enumerable)
                        {
                            currentModel = doc;
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            if (currentModel is null)
            {
                try
                {
                    var frame = TryInvoke(swApp, "Frame");
                    var modelWindow = frame is null ? null : TryInvoke(frame, "ModelWindow");
                    currentModel = modelWindow is null ? currentModel : GetProperty(modelWindow, "ModelDoc") ?? currentModel;
                }
                catch
                {
                }
            }
        }
    }

    private object? ResolveDimension(string name)
    {
        foreach (var candidate in EnumerateDimensionNameCandidates(name))
        {
            var value = TryResolveDimensionByName(candidate);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private object? TryResolveDimensionByName(string dimensionName)
    {
        var methods = new[] { "Parameter", "GetParameter" };
        foreach (var method in methods)
        {
            var value = TryInvoke(currentModel!, method, dimensionName);
            if (value is not null)
            {
                return value;
            }
        }

        var extension = InvokeProperty(currentModel!, "Extension");
        if (extension is null)
        {
            return null;
        }

        var parameter = TryInvoke(extension, "GetParameter", dimensionName);
        if (parameter is not null)
        {
            return parameter;
        }

        var selected = TryInvoke(extension, "SelectByID2", dimensionName, "DIMENSION", 0, 0, 0, false, 0, null, 0);
        if (selected is not bool succeeded || !succeeded)
        {
            return null;
        }

        var selectionManager = InvokeProperty(currentModel!, "SelectionManager");
        if (selectionManager is null || Convert.ToInt32(GetMethodValue(selectionManager, "GetSelectedObjectCount") ?? 0) <= 0)
        {
            return null;
        }

        return TryInvoke(selectionManager, "GetSelectedObject6", 1, -1);
    }

    private IEnumerable<string> EnumerateDimensionNameCandidates(string name)
    {
        yield return name;

        if (!name.Contains('@', StringComparison.Ordinal))
        {
            yield break;
        }

        var docTitle = GetCurrentModelTitleOrPath();
        if (!string.IsNullOrWhiteSpace(docTitle) && !string.Equals(docTitle, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{name}@{docTitle}";
            yield return $"{name}@{Path.GetFileName(docTitle)}";
            yield return $"{name}@{Path.GetFileNameWithoutExtension(docTitle)}";
        }

        var ownerSeparator = name.IndexOf('@');
        if (ownerSeparator <= 0 || ownerSeparator >= name.Length - 1)
        {
            yield break;
        }

        var dimensionId = name[..ownerSeparator];
        var ownerName = name[(ownerSeparator + 1)..];
        if (!dimensionId.StartsWith("D", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        yield return $"RD{dimensionId[1..]}@{ownerName}";
    }

    private void ExecuteExtrusionViaMacro(double depthInMeters, bool reverse)
    {
        var macroDir = Path.Combine(Path.GetTempPath(), "solidworks-mcp-macros");
        var macroPath = Path.Combine(macroDir, $"extrusion_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.swp");
        Directory.CreateDirectory(macroDir);

        var vbaCode = $@"Attribute VB_Name = ""Module1""
Option Explicit

Sub CreateExtrusion()
    Dim swApp As Object
    Dim swModel As Object
    Dim swFeatureMgr As Object
    Dim swFeature As Object

    On Error GoTo ErrorHandler

    Set swApp = Application.SldWorks
    Set swModel = swApp.ActiveDoc

    If swModel Is Nothing Then Exit Sub

    Set swFeatureMgr = swModel.FeatureManager

    Set swFeature = swFeatureMgr.FeatureExtrusion3( _
        True, _
        {(reverse ? "True" : "False")}, _
        False, _
        0, _
        0, _
        {depthInMeters}, _
        0, _
        False, _
        False, _
        False, _
        False, _
        0, _
        0, _
        False, _
        False, _
        False, _
        False, _
        True, _
        False, _
        True, _
        0, _
        0, _
        False _
    )

    swModel.EditRebuild3
    Exit Sub

ErrorHandler:
    Debug.Print ""Extrusion macro error: "" & Err.Description
End Sub
";

        try
        {
            File.WriteAllText(macroPath, vbaCode);
            _ = TryInvoke(swApp!, "RunMacro2", macroPath, "Module1", "CreateExtrusion", 1, 0);
        }
        finally
        {
            try
            {
                if (File.Exists(macroPath))
                {
                    File.Delete(macroPath);
                }
            }
            catch
            {
            }
        }
    }

    private static HashSet<string> CaptureRecentFeatureSignatures(object model)
        => EnumerateRecentFeatures(model).Select(GetFeatureSignature).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static object? ResolveCreatedExtrusionFeature(object model, object? feature, HashSet<string> existingFeatures, IReadOnlyList<object>? recentFeatures = null)
    {
        if (IsExtrusionFeature(feature))
        {
            return feature;
        }

        recentFeatures ??= EnumerateRecentFeatures(model);

        if (TryResolveFeatureFromReturnedObject(feature) is { } resolvedFromReturnedObject)
        {
            return resolvedFromReturnedObject;
        }

        object? latestExtrusion = null;
        object? latestCreatedSolidFeature = null;
        foreach (var candidate in recentFeatures)
        {
            if (IsExtrusionFeature(candidate))
            {
                latestExtrusion ??= candidate;
                if (!existingFeatures.Contains(GetFeatureSignature(candidate)))
                {
                    return candidate;
                }

                continue;
            }

            if (latestCreatedSolidFeature is null
                && !IsSketchFeature(candidate)
                && !IsReferenceFeature(candidate)
                && !existingFeatures.Contains(GetFeatureSignature(candidate)))
            {
                latestCreatedSolidFeature = candidate;
            }
        }

        return latestExtrusion
            ?? latestCreatedSolidFeature
            ?? recentFeatures.FirstOrDefault(candidate => IsExtrusionFeature(candidate) && !IsReferenceFeature(candidate));
    }

    private static object? TryResolveFeatureFromReturnedObject(object? feature)
    {
        if (feature is null)
        {
            return null;
        }

        if (IsExtrusionFeature(feature))
        {
            return feature;
        }

        var specificFeature = TryInvoke(feature, "GetSpecificFeature2");
        if (IsExtrusionFeature(specificFeature))
        {
            return specificFeature;
        }

        var nextFeature = TryInvoke(feature, "GetNextFeature") ?? TryInvoke(feature, "IGetNextFeature");
        if (IsExtrusionFeature(nextFeature))
        {
            return nextFeature;
        }

        return null;
    }

    private static string EnsureSketchSelectionForExtrusion(object model)
    {
        if (GetSelectedObjectCount(model) > 0)
        {
            return "selection already available";
        }

        var activeSketch = GetActiveSketch(model);
        if (TrySelectSketchForExtrusion(model, activeSketch))
        {
            return $"selected active sketch {DescribeFeature(activeSketch)}";
        }

        var recentSketch = EnumerateRecentFeatures(model).FirstOrDefault(IsSketchFeature);
        if (TrySelectSketchForExtrusion(model, recentSketch))
        {
            return $"selected recent sketch {DescribeFeature(recentSketch)}";
        }

        return "unable to restore sketch selection";
    }

    private static int GetSelectedObjectCount(object model)
    {
        var selectionManager = GetProperty(model, "SelectionManager");
        return selectionManager is null
            ? 0
            : Convert.ToInt32(TryInvoke(selectionManager, "GetSelectedObjectCount2", -1)
                ?? GetMethodValue(selectionManager, "GetSelectedObjectCount")
                ?? 0);
    }

    private static object? GetActiveSketch(object model)
    {
        var sketchManager = GetProperty(model, "SketchManager");
        return sketchManager is null
            ? null
            : GetProperty(sketchManager, "ActiveSketch") ?? TryInvoke(sketchManager, "GetActiveSketch2");
    }

    private static bool TrySelectSketchForExtrusion(object model, object? sketch)
    {
        if (TrySelectFeatureLikeObject(sketch))
        {
            return true;
        }

        var sketchName = GetFeatureName(sketch) ?? GetString(sketch, "Name");
        if (string.IsNullOrWhiteSpace(sketchName))
        {
            return false;
        }

        var namedFeature = TryInvoke(model, "FeatureByName", sketchName);
        if (TrySelectFeatureLikeObject(namedFeature))
        {
            return true;
        }

        var extension = GetProperty(model, "Extension");
        var selected = extension is null
            ? null
            : TryInvoke(extension, "SelectByID2", sketchName, "SKETCH", 0d, 0d, 0d, false, 0, null, 0);

        return selected is bool succeeded && succeeded;
    }

    private static bool TrySelectFeatureLikeObject(object? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        var selected = TryInvoke(candidate, "Select2", false, 0)
            ?? TryInvoke(candidate, "Select4", false, null)
            ?? TryInvoke(candidate, "Select", false);

        return selected is bool succeeded && succeeded;
    }

    private static string DescribeExtrusionPreconditions(object model, double depthInMeters, bool reverse, string selectionRecovery)
    {
        var selectionCount = GetSelectedObjectCount(model);
        var activeSketch = GetActiveSketch(model);
        var documentTitle = GetString(model, "GetTitle") ?? "Unknown";
        var documentType = GetSolidWorksDocumentType(model);
        return $"doc={documentTitle}; docType={documentType}; depthMeters={depthInMeters}; reverse={reverse}; selectionCount={selectionCount}; activeSketch={DescribeFeature(activeSketch)}; selectionRecovery={selectionRecovery}";
    }

    private static string DescribeInvocationErrors(IReadOnlyList<string> errors)
        => errors.Count == 0 ? "<none>" : string.Join(" | ", errors.Distinct(StringComparer.Ordinal));

    private static string DescribeFeatures(IEnumerable<object> features)
        => string.Join(", ", features.Select(DescribeFeature));

    private static string DescribeFeature(object? feature)
    {
        if (feature is null)
        {
            return "<null>";
        }

        return $"{GetFeatureName(feature) ?? "<unnamed>"}|{Convert.ToString(GetMethodValue(feature, "GetTypeName2")) ?? "<unknown>"}";
    }

    private static bool IsReferenceFeature(object? feature)
    {
        if (feature is null)
        {
            return true;
        }

        var typeName = Convert.ToString(GetMethodValue(feature, "GetTypeName2")) ?? string.Empty;
        if (typeName.Contains("RefPlane", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Origin", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("CoordSys", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var featureName = GetFeatureName(feature) ?? string.Empty;
        return featureName.Equals("Origin", StringComparison.OrdinalIgnoreCase)
            || featureName.EndsWith(" Plane", StringComparison.OrdinalIgnoreCase)
            || featureName.Equals("Annotations", StringComparison.OrdinalIgnoreCase)
            || featureName.Equals("History", StringComparison.OrdinalIgnoreCase)
            || featureName.Equals("Sensors", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaneFeature(object? feature)
        => feature is not null && (Convert.ToString(GetMethodValue(feature, "GetTypeName2")) ?? string.Empty).Contains("RefPlane", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<object> EnumerateRecentFeatures(object model, int maxFeatures = 25)
    {
        List<object> features = [];
        var featureCountValue = TryInvoke(model, "GetFeatureCount");
        if (featureCountValue is not null && int.TryParse(Convert.ToString(featureCountValue), out var featureCount) && featureCount > 0)
        {
            for (var index = 0; index < Math.Min(featureCount, maxFeatures); index++)
            {
                var feature = TryInvoke(model, "FeatureByPositionReverse", index);
                if (feature is not null)
                {
                    features.Add(feature);
                }
            }
        }

        if (features.Count > 0)
        {
            return features;
        }

        var current = TryInvoke(model, "FirstFeature") ?? TryInvoke(model, "IFirstFeature");
        while (current is not null && features.Count < maxFeatures)
        {
            features.Add(current);
            current = TryInvoke(current, "GetNextFeature") ?? TryInvoke(current, "IGetNextFeature");
        }

        features.Reverse();
        return features;
    }

    private static bool IsExtrusionFeature(object? feature)
    {
        if (feature is null)
        {
            return false;
        }

        var typeName = Convert.ToString(GetMethodValue(feature, "GetTypeName2")) ?? string.Empty;
        if (IsSketchType(typeName))
        {
            return false;
        }

        if (typeName.Contains("Extrud", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Boss", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("BaseBody", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var featureName = GetFeatureName(feature) ?? string.Empty;
        return featureName.Contains("Extrude", StringComparison.OrdinalIgnoreCase)
            || featureName.Contains("Boss-", StringComparison.OrdinalIgnoreCase);
    }

    private object? ResolveCreatedFeature(object model, object? directReturn, ISet<string> existingFeatures)
    {
        if (directReturn is not null && !existingFeatures.Contains(GetFeatureSignature(directReturn)))
        {
            return directReturn;
        }

        foreach (var candidate in EnumerateRecentFeatures(model, 500))
        {
            if (!existingFeatures.Contains(GetFeatureSignature(candidate)))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void TryRenameFeature(object feature, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            SetProperty(feature, "Name", name);
        }
        catch
        {
        }
    }

    private static bool IsSketchFeature(object? feature)
        => feature is not null && IsSketchType(Convert.ToString(GetMethodValue(feature, "GetTypeName2")) ?? string.Empty);

    private static bool IsSketchType(string typeName)
        => typeName.Equals("ProfileFeature", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Sketch", StringComparison.OrdinalIgnoreCase);

    private static string GetFeatureSignature(object feature)
        => $"{GetFeatureName(feature) ?? string.Empty}|{Convert.ToString(GetMethodValue(feature, "GetTypeName2")) ?? string.Empty}";

    private static string? GetFeatureName(object? feature)
        => GetString(feature, "Name") ?? Convert.ToString(GetMethodValue(feature, "GetName"));

    private static object? GetProperty(object? target, string propertyName)
    {
        if (target is null)
        {
            return null;
        }

        var type = target.GetType();
        var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is not null)
        {
            return property.GetValue(target);
        }

        try
        {
            return type.InvokeMember(propertyName, BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, target, null);
        }
        catch
        {
        }

        try
        {
            return type.InvokeMember($"get_{propertyName}", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, target, null);
        }
        catch
        {
            return null;
        }
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var type = target.GetType();
        var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(target, value);
        }
    }

    private static object? InvokeProperty(object target, string propertyName)
        => GetProperty(target, propertyName);

    private static object? Invoke(object target, string methodName, params object?[] args)
    {
        return TryInvoke(target, methodName, args);
    }

    private static bool TryInvokeCommand(object target, List<string> attempted, string methodName, params object?[] args)
    {
        attempted.Add(methodName);
        return TryInvokeMember(target, methodName, out var result, args)
            && (result is not bool boolResult || boolResult);
    }

    private static object? TryInvokeWithDiagnostics(object? target, List<string> attempted, List<string> errors, string methodName, params object?[] args)
    {
        attempted.Add(methodName);
        return TryInvokeMember(target, methodName, out var result, errors, args) ? result : null;
    }

    private static object? TryInvoke(object? target, string methodName, params object?[] args)
        => TryInvokeMember(target, methodName, out var result, args) ? result : null;

    private static bool TryInvokeMember(object? target, string methodName, out object? result, params object?[] args)
    {
        List<string> ignoredErrors = [];
        return TryInvokeMember(target, methodName, out result, ignoredErrors, args);
    }

    private static bool TryInvokeMember(object? target, string methodName, out object? result, List<string> errors, params object?[] args)
    {
        result = null;
        if (target is null)
        {
            return false;
        }

        var targetType = target.GetType();

        var candidateMethods = targetType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase))
            .Select(method => new { Method = method, Score = GetMethodScore(method, args) })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .Select(item => item.Method)
            .ToArray();

        foreach (var method in candidateMethods)
        {
            try
            {
                result = method.Invoke(target, args);
                return true;
            }
            catch (TargetInvocationException ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                errors.Add($"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))}) => {message}");
            }
            catch (Exception ex)
            {
                errors.Add($"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))}) => {ex.Message}");
            }
        }

        if (methodName is "FeatureManager" or "SketchManager" or "Extension" or "SelectionManager" or "ActiveDoc")
        {
            return false;
        }

        try
        {
            result = targetType.InvokeMember(methodName, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, target, args);
            return true;
        }
        catch (Exception ex)
        {
            errors.Add($"late-bound {methodName} => {ex.Message}");
            return false;
        }
    }

    private static int GetMethodScore(MethodInfo method, object?[] args)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != args.Length)
        {
            return -1;
        }

        var score = 0;
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameterType = parameters[index].ParameterType;
            if (parameterType.IsByRef)
            {
                parameterType = parameterType.GetElementType() ?? parameterType;
            }

            var argument = args[index];
            if (argument is null)
            {
                if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) is null)
                {
                    return -1;
                }

                score += 1;
                continue;
            }

            var argumentType = argument.GetType();
            if (parameterType.IsAssignableFrom(argumentType))
            {
                score += 3;
                continue;
            }

            if (CanConvertArgument(argument, parameterType))
            {
                score += 2;
                continue;
            }

            return -1;
        }

        return score;
    }

    private static bool CanConvertArgument(object argument, Type destinationType)
    {
        try
        {
            if (destinationType.IsEnum)
            {
                _ = Enum.ToObject(destinationType, argument);
                return true;
            }

            _ = Convert.ChangeType(argument, destinationType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetString(object? target, string memberName)
    {
        if (target is null)
        {
            return null;
        }

        var value = GetProperty(target, memberName) ?? TryInvoke(target, memberName);
        return value?.ToString();
    }

    private static object? GetMethodValue(object? target, string methodName)
    {
        return TryInvoke(target, methodName);
    }

    private static double GetNumber(Dictionary<string, object?> dictionary, string key, double defaultValue)
    {
        return dictionary.TryGetValue(key, out var value) && double.TryParse(Convert.ToString(value), out var parsed) ? parsed : defaultValue;
    }

    private static double GetNumberFromObject(object target, string propertyName, double defaultValue)
    {
        var value = GetProperty(target, propertyName);
        return double.TryParse(Convert.ToString(value), out var parsed) ? parsed : defaultValue;
    }

    private static double GetNumberFromMethod(object target, string methodName, double defaultValue)
    {
        var value = TryInvoke(target, methodName);
        return double.TryParse(Convert.ToString(value), out var parsed) ? parsed : defaultValue;
    }

    private static bool GetBool(Dictionary<string, object?> dictionary, string key, bool defaultValue)
    {
        return dictionary.TryGetValue(key, out var value) && bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : defaultValue;
    }

    private static double[] GetArrayFromMethod(object target, string methodName, params object?[] args)
    {
        var value = TryInvoke(target, methodName, args);
        return value is System.Collections.IEnumerable enumerable
            ? enumerable.Cast<object?>().Select(item => double.TryParse(Convert.ToString(item), out var parsed) ? parsed : 0d).ToArray()
            : [];
    }

    private static double[] GetArrayFromObject(object target, string propertyName)
    {
        var value = GetProperty(target, propertyName);
        return value is System.Collections.IEnumerable enumerable
            ? enumerable.Cast<object?>().Select(item => double.TryParse(Convert.ToString(item), out var parsed) ? parsed : 0d).ToArray()
            : [];
    }

    private static object? GetEquationAt(object equationManager, int index)
    {
        try
        {
            var property = equationManager.GetType().GetProperty("Equation");
            var value = property?.GetValue(equationManager);
            if (value is System.Collections.IList list && index >= 0 && index < list.Count)
            {
                return list[index];
            }
        }
        catch
        {
        }

        return null;
    }

    private static void SetEquationAt(object equationManager, int index, string equation)
    {
        try
        {
            var property = equationManager.GetType().GetProperty("Equation");
            var value = property?.GetValue(equationManager);
            if (value is System.Collections.IList list && index >= 0 && index < list.Count)
            {
                list[index] = equation;
            }
        }
        catch
        {
        }
    }

    private static List<string>? CreateDevelopmentDebugTrace()
    {
#if DEBUG
        return new List<string>();
#else
        return null;
#endif
    }

    [Conditional("DEBUG")]
    private static void AddDebugTrace(List<string>? debugTrace, string message)
    {
        if (debugTrace is null)
        {
            return;
        }

        debugTrace.Add(message);
    }

    [Conditional("DEBUG")]
    private static void AttachDebugTrace(Dictionary<string, object?> result, List<string>? debugTrace)
    {
        if (debugTrace is null || debugTrace.Count == 0)
        {
            return;
        }

        result["debug"] = debugTrace.Cast<object?>().ToList();
    }

    private bool TrySelectSketchPlane(object model, string? plane, List<string>? debugTrace)
    {
        var normalizedPlane = NormalizePrimaryPlaneName(plane);
        AddDebugTrace(debugTrace, $"Normalized plane: {normalizedPlane}");

        var candidates = EnumeratePlaneNameCandidates(normalizedPlane).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        AddDebugTrace(debugTrace, $"Plane candidates: {string.Join(", ", candidates)}");

        foreach (var candidate in candidates)
        {
            var feature = TryInvoke(model, "FeatureByName", candidate);
            AddDebugTrace(debugTrace, $"FeatureByName('{candidate}') => {(feature is null ? "null" : feature.GetType().FullName)}");
            if (TrySelectResolvedPlane(feature, candidate, debugTrace))
            {
                return true;
            }
        }

        var recentFeatures = EnumerateRecentFeatures(model);
        AddDebugTrace(debugTrace, $"Recent feature enumeration count: {recentFeatures.Count}");
        foreach (var feature in recentFeatures)
        {
            var featureName = GetFeatureName(feature);
            if (string.IsNullOrWhiteSpace(featureName) || !candidates.Contains(featureName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            AddDebugTrace(debugTrace, $"Recent feature match: '{featureName}'.");
            if (TrySelectResolvedPlane(feature, featureName, debugTrace))
            {
                return true;
            }
        }

        var extension = GetProperty(model, "Extension");
        if (extension is not null)
        {
            AddDebugTrace(debugTrace, $"Model extension type: {extension.GetType().FullName}");
            foreach (var candidate in candidates)
            {
                var selected = TryInvoke(extension, "SelectByID2", candidate, "PLANE", 0d, 0d, 0d, false, 0, null, 0);
                AddDebugTrace(debugTrace, $"SelectByID2('{candidate}', 'PLANE') => {FormatDebugValue(selected)}");
                if (selected is bool succeeded && succeeded)
                {
                    return true;
                }
            }
        }
        else
        {
            AddDebugTrace(debugTrace, "Model extension is null.");
        }

        var featureManager = GetProperty(model, "FeatureManager");
        if (featureManager is null)
        {
            AddDebugTrace(debugTrace, "FeatureManager is null.");
            return false;
        }

        AddDebugTrace(debugTrace, $"FeatureManager type: {featureManager.GetType().FullName}");

        foreach (var candidate in candidates)
        {
            var planeRef = TryInvoke(featureManager, "GetPlane", candidate);
            AddDebugTrace(debugTrace, $"GetPlane('{candidate}') => {(planeRef is null ? "null" : planeRef.GetType().FullName)}");
            if (planeRef is null)
            {
                continue;
            }

            if (TrySelectResolvedPlane(planeRef, candidate, debugTrace))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySelectResolvedPlane(object? planeRef, string candidate, List<string>? debugTrace)
    {
        if (planeRef is null)
        {
            return false;
        }

        var selected = TryInvoke(planeRef, "Select2", false, 0)
            ?? TryInvoke(planeRef, "Select4", false, null)
            ?? TryInvoke(planeRef, "Select", false);

        AddDebugTrace(debugTrace, $"Plane.Select('{candidate}') => {FormatDebugValue(selected)}");
        if (selected is bool succeeded && succeeded)
        {
            return true;
        }

        var specificPlane = TryInvoke(planeRef, "GetSpecificFeature2");
        AddDebugTrace(debugTrace, $"GetSpecificFeature2('{candidate}') => {(specificPlane is null ? "null" : specificPlane.GetType().FullName)}");
        if (specificPlane is null)
        {
            return false;
        }

        selected = TryInvoke(specificPlane, "Select2", false, 0)
            ?? TryInvoke(specificPlane, "Select4", false, null)
            ?? TryInvoke(specificPlane, "Select", false);

        AddDebugTrace(debugTrace, $"SpecificPlane.Select('{candidate}') => {FormatDebugValue(selected)}");
        return selected is bool specificSelected && specificSelected;
    }

    private static string FormatDebugValue(object? value)
        => value is null ? "null" : Convert.ToString(value) ?? value.GetType().FullName ?? string.Empty;

    private static bool IsAssemblyDocument(object model)
        => GetSolidWorksDocumentType(model) == 2;

    private static bool IsPartDocument(object model)
        => GetSolidWorksDocumentType(model) == 1;

    private static bool IsAxisFeature(object? feature)
        => feature is not null && (Convert.ToString(GetMethodValue(feature, "GetTypeName2")) ?? string.Empty).Contains("RefAxis", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<object> GetModelBodies(object model)
    {
        var bodies = AsEnumerable(TryInvoke(model, "GetBodies2", 0, false));
        if (bodies.Count > 0)
        {
            return bodies;
        }

        bodies = AsEnumerable(TryInvoke(model, "GetBodies2", 0));
        if (bodies.Count > 0)
        {
            return bodies;
        }

        bodies = AsEnumerable(TryInvoke(model, "GetBodies"));
        if (bodies.Count > 0)
        {
            return bodies;
        }

        if (IsAssemblyDocument(model))
        {
            var assemblyBodies = new List<object>();
            foreach (var component in AsEnumerable(TryInvoke(model, "GetComponents", true)))
            {
                assemblyBodies.AddRange(AsEnumerable(TryInvoke(component, "GetBodies2", 0) ?? TryInvoke(component, "GetBodies")));
            }

            return assemblyBodies;
        }

        return [];
    }

    private static double[] TryGetBodyBox(object body)
        => GetArrayFromMethod(body, "GetBodyBox");

    private static (double X, double Y, double Z) GetFacePickPoint(object face)
    {
        var box = GetArrayFromMethod(face, "GetBox");
        if (box.Length >= 6)
        {
            return ((box[0] + box[3]) / 2d, (box[1] + box[4]) / 2d, (box[2] + box[5]) / 2d);
        }

        var point = GetArrayFromMethod(face, "GetClosestPointOn", 0d, 0d, 0d);
        return point.Length >= 3 ? (point[0], point[1], point[2]) : (0d, 0d, 0d);
    }

    private static string NormalizeSurfaceType(int code)
        => code switch
        {
            4001 => "plane",
            4002 => "cylinder",
            4003 => "cone",
            4004 => "sphere",
            4005 => "torus",
            4006 => "bsurface",
            _ => $"surface_{code}",
        };

    private static string NormalizeCurveType(int code)
        => code switch
        {
            3001 => "line",
            3002 => "circle",
            3003 => "ellipse",
            3005 => "bcurve",
            _ => $"curve_{code}",
        };

    private static Dictionary<string, object?> CreatePointDictionary(double x, double y, double z)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = Math.Round(x, 6),
            ["y"] = Math.Round(y, 6),
            ["z"] = Math.Round(z, 6),
        };

    private static double? ReadMeasureValue(object measure, string propertyName, double scale)
    {
        var raw = GetProperty(measure, propertyName);
        if (raw is null)
        {
            return null;
        }

        if (!double.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), out var value))
        {
            return null;
        }

        return value < 0 ? null : Math.Round(value * scale, 6);
    }

    private static bool TrySetProperty(object target, string propertyName, object? value)
    {
        try
        {
            SetProperty(target, propertyName, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizePrimaryPlaneName(string? plane)
    {
        if (string.IsNullOrWhiteSpace(plane))
        {
            return "Front Plane";
        }

        return plane.Trim() switch
        {
            "Front" => "Front Plane",
            "Top" => "Top Plane",
            "Right" => "Right Plane",
            _ => plane.Trim(),
        };
    }

    private static IEnumerable<string> EnumeratePlaneNameCandidates(string normalizedPlane)
    {
        yield return normalizedPlane;

        if (normalizedPlane.EndsWith(" Plane", StringComparison.OrdinalIgnoreCase))
        {
            yield return normalizedPlane[..^6];
            yield break;
        }

        yield return normalizedPlane + " Plane";
    }

    private object? ResolveSketch(string? sketchName)
    {
        if (currentModel is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(sketchName))
        {
            var named = TryInvoke(currentModel, "FeatureByName", sketchName);
            if (IsSketchFeature(named))
            {
                return named;
            }
        }

        var activeSketch = GetActiveSketch(currentModel);
        if (activeSketch is not null)
        {
            return activeSketch;
        }

        return EnumerateRecentFeatures(currentModel).FirstOrDefault(IsSketchFeature);
    }

    private IEnumerable<object> ResolveSketchRelationEntities(SelectionSpec selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var sketch = ResolveSketch(selection.SketchName) ?? throw new InvalidOperationException($"Sketch not found: {selection.SketchName ?? "<active>"}");
        var specificSketch = TryInvoke(sketch, "GetSpecificFeature2") ?? sketch;

        foreach (var handle in selection.SketchSegmentHandles ?? [])
        {
            var segment = GetSketchEntityByHandle(specificSketch, "GetSketchSegments", "GetSketchSegments2", handle, "sketch segment");
            yield return segment;
        }

        foreach (var index in selection.SketchSegments ?? [])
        {
            var segment = GetSketchEntityAtIndex(specificSketch, "GetSketchSegments", "GetSketchSegments2", index, "sketch segment");
            yield return segment;
        }

        foreach (var handle in selection.SketchPointHandles ?? [])
        {
            var point = GetSketchEntityByHandle(specificSketch, "GetSketchPoints2", "GetSketchPoints", handle, "sketch point");
            yield return point;
        }

        foreach (var index in selection.SketchPoints ?? [])
        {
            var point = GetSketchEntityAtIndex(specificSketch, "GetSketchPoints2", "GetSketchPoints", index, "sketch point");
            yield return point;
        }
    }

    private static object GetSketchEntityAtIndex(object sketch, string primaryMethod, string secondaryMethod, int index, string label)
    {
        var entries = AsEnumerable(TryInvoke(sketch, primaryMethod) ?? TryInvoke(sketch, secondaryMethod));
        if (index < 0 || index >= entries.Count)
        {
            throw new InvalidOperationException($"{label} index {index} is out of range (0..{entries.Count - 1}).");
        }

        return entries[index];
    }

    private static object GetSketchEntityByHandle(object sketch, string primaryMethod, string secondaryMethod, string handle, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        var entries = AsEnumerable(TryInvoke(sketch, primaryMethod) ?? TryInvoke(sketch, secondaryMethod));
        var normalizedHandle = handle.Trim();

        if (TryParseSketchEntityHandleIndex(normalizedHandle, out var parsedIndex) && parsedIndex >= 0 && parsedIndex < entries.Count)
        {
            return entries[parsedIndex];
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var candidate = entries[index];
            var candidateName = Convert.ToString(GetMethodValue(candidate, "GetNameForSelection")) ?? string.Empty;
            if (string.Equals(candidateName, normalizedHandle, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"{label} handle '{handle}' was not found in the target sketch.");
    }

    private static bool TryParseSketchEntityHandleIndex(string handle, out int index)
    {
        index = -1;
        var colonIndex = handle.LastIndexOf(':');
        var token = colonIndex >= 0 ? handle[(colonIndex + 1)..] : handle;

        if (token.StartsWith("Line", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("Point", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("Arc", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("Circle", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("Spline", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("Ellipse", StringComparison.OrdinalIgnoreCase))
        {
            var digits = new string(token.SkipWhile(ch => !char.IsDigit(ch)).ToArray());
            if (int.TryParse(digits, out var oneBasedIndex) && oneBasedIndex > 0)
            {
                index = oneBasedIndex - 1;
                return true;
            }
        }

        return int.TryParse(token, out index);
    }

    private static bool TrySelectEntity(object entity, bool append)
    {
        var selected = TryInvoke(entity, "Select4", append, null)
            ?? TryInvoke(entity, "Select2", append, 0)
            ?? TryInvoke(entity, "Select", append);

        return selected is bool succeeded && succeeded;
    }

    private void SetDimensionValueCore(object dimension, double systemValue)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        if (HasProperty(dimension, "SystemValue"))
        {
            SetProperty(dimension, "SystemValue", systemValue);
            return;
        }

        if (HasProperty(dimension, "Value"))
        {
            SetProperty(dimension, "Value", systemValue);
            return;
        }

        if (TryInvoke(dimension, "SetSystemValue", systemValue) is bool systemValueSet && systemValueSet)
        {
            return;
        }

        if (TryInvoke(dimension, "SetValue", systemValue) is bool valueSet && valueSet)
        {
            return;
        }

        throw new InvalidOperationException("Failed to update dimension value.");
    }

    private static List<object> AsEnumerable(object? value)
    {
        return value is System.Collections.IEnumerable enumerable
            ? enumerable.Cast<object?>().Where(item => item is not null).Cast<object>().ToList()
            : [];
    }

    private static string BuildEntityHandle(string kind, string name)
        => $"{kind}:{name}";

    private static string? NormalizeOutputRoot(string? outputRoot)
    {
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            return null;
        }

        return Path.GetFullPath(outputRoot.Trim());
    }

    private string NormalizeManagedOutputPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            return fullPath;
        }

        var rootWithSeparator = outputRoot.EndsWith(Path.DirectorySeparatorChar)
            ? outputRoot
            : outputRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, outputRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Output path is outside SW_MCP_OUTPUT_ROOT: {fullPath}");
        }

        return fullPath;
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void TryClearSelection()
    {
        TryInvoke(currentModel!, "ClearSelection2", true);
    }

    private static object? GetPropertyValue(object target, string propertyName) => GetProperty(target, propertyName);

    private static bool HasProperty(object target, string propertyName)
        => target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase) is not null;
}
