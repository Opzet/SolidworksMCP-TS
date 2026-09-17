namespace SolidworksMCP;

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

        var title = GetString(currentModel, "GetTitle") ?? GetString(currentModel, "GetPathName") ?? "Unknown";

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
        currentModel = Invoke(swApp!, "NewPart") ?? TryCreatePartFromTemplate(swApp!);
        if (currentModel is null)
        {
            throw new InvalidOperationException("Failed to create new part - no template available");
        }

        return new SolidWorksModel
        {
            Path = string.Empty,
            Name = Convert.ToString(GetMethodValue(currentModel, "GetTitle")) ?? "Part",
            Type = "Part",
            IsActive = true,
        };
    }

    public object CreateSketch(Dictionary<string, object?> parameters)
    {
        EnsureCurrentModel();
        var plane = parameters.TryGetValue("plane", out var planeValue) ? Convert.ToString(planeValue) : "Front";
        var featureManager = InvokeProperty(currentModel!, "FeatureManager");
        var planeRef = featureManager is null ? null : TryInvoke(featureManager, "GetPlane", plane ?? "Front");
        if (planeRef is not null)
        {
            var sketchManager = InvokeProperty(currentModel!, "SketchManager");
            TryInvoke(sketchManager, "InsertSketch", true);
            var sketch = GetProperty(sketchManager, "ActiveSketch");
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["sketchId"] = GetString(sketch, "Name") ?? "Sketch",
            };
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = false,
            ["error"] = "Failed to create sketch",
        };
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

    public SolidWorksFeature CreateExtrude(double depth, double draft = 0, bool reverse = false)
    {
        _ = draft;
        EnsureCurrentModel();
        if (currentModel is null)
        {
            throw new InvalidOperationException("No model open");
        }

        var featureManager = InvokeProperty(currentModel, "FeatureManager") ?? throw new InvalidOperationException("Cannot access FeatureManager");
        TryClearSelection();

        var depthInMeters = depth / 1000d;
        object? feature = null;

        try
        {
            feature = TryInvoke(featureManager, "FeatureExtrusion", true, reverse, false, 0, 0, depthInMeters, 0, false, false, false, false, 0, 0)
                ?? TryInvoke(featureManager, "FeatureExtrusion3", true, reverse, false, 0, 0, depthInMeters, 0, false, false, false, false, 0, 0, false, false, false, false, true, false, true, 0, 0, false);
        }
        catch
        {
            feature = null;
        }

        if (feature is null)
        {
            feature = ExecuteExtrusionViaMacro(depthInMeters, reverse);
        }

        if (feature is null)
        {
            throw new InvalidOperationException("Failed to create extrusion - feature is null");
        }

        var featureName = GetString(feature, "Name") ?? Convert.ToString(GetMethodValue(feature, "GetName")) ?? "Boss-Extrude1";
        TryClearSelection();
        if (TryInvoke(currentModel, "EditRebuild3") is null)
        {
            _ = TryInvoke(currentModel, "EditRebuild");
        }

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

        var currentPath = GetString(currentModel, "GetPathName");
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            var docType = Convert.ToInt32(GetMethodValue(currentModel, "GetType") ?? 0);
            var ext = docType switch
            {
                1 => ".SLDPRT",
                2 => ".SLDASM",
                _ => ".SLDDRW",
            };
            var tempPath = Path.ChangeExtension(filePath, ext);
            TryInvoke(currentModel, "SaveAs3", tempPath, 0, 1);
        }

        var extName = format.ToLowerInvariant();
        var success = extName switch
        {
            "step" or "stp" => TryInvoke(currentModel, "SaveAs3", filePath, 0, 2) is true || TryInvoke(InvokeProperty(currentModel, "Extension"), "SaveAs", filePath, 0, 2, null, 0, 0) is true,
            "iges" or "igs" => TryInvoke(currentModel, "SaveAs3", filePath, 0, 2) is true || TryInvoke(InvokeProperty(currentModel, "Extension"), "SaveAs", filePath, 0, 2, null, 0, 0) is true,
            "stl" => TryInvoke(currentModel, "SaveAs3", filePath, 0, 2) is true || TryInvoke(currentModel, "SaveAs4", filePath, 0, 2, 0, 0) is true || TryInvoke(InvokeProperty(currentModel, "Extension"), "SaveAs", filePath, 0, 2, null, 0, 0) is true,
            "pdf" => Convert.ToInt32(GetMethodValue(currentModel, "GetType") ?? 0) == 3 && (TryInvoke(currentModel, "SaveAs3", filePath, 0, 2) is true || TryInvoke(InvokeProperty(currentModel, "Extension"), "SaveAs", filePath, 0, 2, null, 0, 0) is true),
            "dxf" or "dwg" => TryInvoke(currentModel, "SaveAs3", filePath, 0, 2) is true || TryInvoke(InvokeProperty(currentModel, "Extension"), "SaveAs", filePath, 0, 2, null, 0, 0) is true,
            _ => throw new InvalidOperationException($"Unsupported export format: {format}"),
        };

        if (!success)
        {
            throw new InvalidOperationException($"Failed to export to {format.ToUpperInvariant()}: Export returned false");
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

    private static object? TryCreatePartFromTemplate(object swApplication)
    {
        ArgumentNullException.ThrowIfNull(swApplication);

        var templateCandidates = new List<string>();

        var envTemplate = Environment.GetEnvironmentVariable("SOLIDWORKS_PART_TEMPLATE");
        if (!string.IsNullOrWhiteSpace(envTemplate))
        {
            templateCandidates.Add(envTemplate);
        }

        var commonTemplateRoots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "SOLIDWORKS", "SOLIDWORKS 2024", "templates"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "SOLIDWORKS", "SOLIDWORKS 2023", "templates"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOLIDWORKS", "templates"),
        };

        foreach (var root in commonTemplateRoots.Where(Directory.Exists))
        {
            try
            {
                var candidate = Directory.EnumerateFiles(root, "*.prtdot", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    templateCandidates.Add(candidate);
                }
            }
            catch
            {
                // ignore inaccessible template folders
            }
        }

        foreach (var templatePath in templateCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var created = TryInvoke(swApplication, "NewDocument", templatePath, 0, 0d, 0d);
            if (created is not null)
            {
                return created;
            }
        }

        var defaultTemplate = Convert.ToString(TryInvoke(swApplication, "GetUserPreferenceStringValue", 13));
        if (!string.IsNullOrWhiteSpace(defaultTemplate))
        {
            var created = TryInvoke(swApplication, "NewDocument", defaultTemplate, 0, 0d, 0d);
            if (created is not null)
            {
                return created;
            }
        }

        return TryInvoke(swApplication, "NewDocument", string.Empty, 0, 0d, 0d);
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
        var methods = new[] { "Parameter", "GetParameter" };
        foreach (var method in methods)
        {
            var value = TryInvoke(currentModel!, method, name);
            if (value is not null)
            {
                return value;
            }
        }

        var extension = InvokeProperty(currentModel!, "Extension");
        if (extension is not null)
        {
            var value = TryInvoke(extension, "GetParameter", name);
            if (value is not null)
            {
                return value;
            }

            var selected = TryInvoke(extension, "SelectByID2", name, "DIMENSION", 0, 0, 0, false, 0, null, 0);
            if (selected is true)
            {
                var selectionManager = InvokeProperty(currentModel!, "SelectionManager");
                if (selectionManager is not null && Convert.ToInt32(GetMethodValue(selectionManager, "GetSelectedObjectCount") ?? 0) > 0)
                {
                    return TryInvoke(selectionManager, "GetSelectedObject6", 1, -1);
                }
            }
        }

        return null;
    }

    private object? ExecuteExtrusionViaMacro(double depthInMeters, bool reverse)
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
            TryInvoke(swApp!, "RunMacro2", macroPath, "Module1", "CreateExtrusion", 1, 0);
            return TryInvoke(currentModel!, "FeatureByPositionReverse", 0);
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

    private static object? GetProperty(object? target, string propertyName)
    {
        if (target is null)
        {
            return null;
        }

        var type = target.GetType();
        var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(target);
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

    private object? InvokeProperty(object target, string propertyName)
        => GetProperty(target, propertyName);

    private static object? Invoke(object target, string methodName, params object?[] args)
    {
        return TryInvoke(target, methodName, args);
    }

    private static object? TryInvoke(object? target, string methodName, params object?[] args)
    {
        if (target is null)
        {
            return null;
        }

        try
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (method is not null)
            {
                return method.Invoke(target, args);
            }

            dynamic dynamicTarget = target;
            return methodName switch
            {
                "FeatureManager" or "SketchManager" or "Extension" or "SelectionManager" or "ActiveDoc" => null,
                _ => dynamicTarget.GetType().InvokeMember(methodName, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, target, args),
            };
        }
        catch
        {
            return null;
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

    private void TryClearSelection()
    {
        TryInvoke(currentModel!, "ClearSelection2", true);
    }

    private static object? GetPropertyValue(object target, string propertyName) => GetProperty(target, propertyName);

    private static bool HasProperty(object target, string propertyName)
        => target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase) is not null;
}
