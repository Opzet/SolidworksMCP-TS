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

    private object? swApp;
    private object? currentModel;
    private string? lastDrawingSourceModelPath;

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

    private static bool TrySaveDocument(object model, string filePath, int options)
    {
        var extension = GetProperty(model, "Extension");
        return TryInvoke(model, "SaveAs3", filePath, 0, options) is true
            || TryInvoke(model, "SaveAs4", filePath, 0, options, 0, 0) is true
            || TryInvoke(extension, "SaveAs", filePath, 0, options, null, 0, 0) is true;
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

    private void TryClearSelection()
    {
        TryInvoke(currentModel!, "ClearSelection2", true);
    }

    private static object? GetPropertyValue(object target, string propertyName) => GetProperty(target, propertyName);

    private static bool HasProperty(object target, string propertyName)
        => target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase) is not null;
}
