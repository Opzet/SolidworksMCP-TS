namespace SolidworksMCP.Tests;

using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SolidWorksApiDrawingAndExportTests
{
    [TestMethod]
    public void CreateDrawingFromCurrentModelUsesResolvedTemplateAndTracksSavedSourcePath()
    {
        FakeSourceModel sourceModel = new();
        FakeDrawingDocument drawing = new();
        FakeSolidWorksApp app = new(drawing);
        SolidWorksApi api = CreateApi(sourceModel, app);

        Dictionary<string, object?> result = (Dictionary<string, object?>)api.CreateDrawingFromCurrentModel(templatePath: null);

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual("C:\\Templates\\Default.drwdot", result["templatePath"]);
        Assert.AreEqual(1, app.NewDocumentCount);
        StringAssert.EndsWith(sourceModel.LastSavedPath!, ".sldprt");
        Assert.AreEqual(sourceModel.LastSavedPath, result["sourceModelPath"]);
        Assert.AreEqual(sourceModel.LastSavedPath, drawing.LastViewModelPath);
    }

    [TestMethod]
    public void AddDrawingViewUsesTrackedSourceModelPathWhenModelPathIsOmitted()
    {
        FakeSourceModel sourceModel = new();
        FakeDrawingDocument drawing = new();
        FakeSolidWorksApp app = new(drawing);
        SolidWorksApi api = CreateApi(sourceModel, app);
        _ = api.CreateDrawingFromCurrentModel(templatePath: null);

        Dictionary<string, object?> result = (Dictionary<string, object?>)api.AddDrawingView(new Dictionary<string, object?>
        {
            ["viewType"] = "iso",
            ["x"] = 25,
            ["y"] = 40,
            ["scale"] = 2,
        });

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual(sourceModel.LastSavedPath, result["modelPath"]);
        Assert.AreEqual("*Isometric", drawing.LastViewName);
        Assert.AreEqual(2d, drawing.LastView.ScaleDecimal);
    }

    [TestMethod]
    public async Task ExportFileInfersAliasFormatAndSavesUnsavedModelWithoutTypeCastFailure()
    {
        FakeExportModel model = new();
        SolidWorksApi api = CreateApi(model, app: null);
        McpToolDefinition tool = ExportTools.GetTools().Single(item => item.Name == "export_file");
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["outputPath"] = "C:\\Exports\\4_bar_linkage.stp",
        }));

        var result = await tool.Handler(document.RootElement, api, CancellationToken.None);

        Assert.IsInstanceOfType<string>(result);
        StringAssert.Contains(result!.ToString()!, "Exported to STP");
        CollectionAssert.AreEqual(
            new[] { model.InitialSavePath!, "C:\\Exports\\4_bar_linkage.stp" },
            model.SaveAs3Calls);
        StringAssert.EndsWith(model.InitialSavePath!, ".sldprt");
    }

    [TestMethod]
    public void GetDrawingCapabilitiesReportsApiAndFallbackPolicy()
    {
        FakeDrawingDocument drawing = new();
        FakeSolidWorksApp app = new(drawing);
        SolidWorksApi api = CreateApi(drawing, app);

        var result = api.GetDrawingCapabilities();

        Assert.AreEqual(false, ((Dictionary<string, object?>)result["fallbackStrategy"])["uiAutomationDefault"]);
        Assert.AreEqual(true, ((Dictionary<string, object?>)result["api"])["selectById2"]);
    }

    [TestMethod]
    public void AddSectionViewFallsBackToMacroWhenApiSelectionFails()
    {
        FakeDrawingDocument drawing = new();
        FakeSolidWorksApp app = new(drawing);
        SolidWorksApi api = CreateApi(drawing, app);

        var result = api.AddSectionView(new Dictionary<string, object?>
        {
            ["parentView"] = "MissingParentView",
            ["xMm"] = 100d,
            ["yMm"] = 60d,
            ["x1Mm"] = 10d,
            ["y1Mm"] = 10d,
            ["x2Mm"] = 30d,
            ["y2Mm"] = 30d,
            ["label"] = "A-A",
            ["fallbackMacroPath"] = "C:\\Macros\\drawing-fallback.swp",
            ["fallbackModuleName"] = "Main",
            ["fallbackProcedureName"] = "CreateSectionFallback",
        });

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual("macro-fallback", result["strategy"]);
        Assert.AreEqual("C:\\Macros\\drawing-fallback.swp", app.LastRunMacroPath);
    }

    private static SolidWorksApi CreateApi(object model, object? app)
    {
        SolidWorksApi api = new();
        SetPrivateField(api, "currentModel", model);
        SetPrivateField(api, "swApp", app);
        return api;
    }

    private static void SetPrivateField(SolidWorksApi api, string fieldName, object? value)
    {
        FieldInfo? field = typeof(SolidWorksApi).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(api, value);
    }

    private sealed class FakeSolidWorksApp(FakeDrawingDocument drawing)
    {
        public int NewDocumentCount { get; private set; }

        public string? LastRunMacroPath { get; private set; }

        public string GetDocumentTemplate(int documentType, int paperSize, double width, double height)
        {
            _ = paperSize;
            _ = width;
            _ = height;
            return documentType == 3 ? "C:\\Templates\\Default.drwdot" : string.Empty;
        }

        public object? NewDocument(string templatePath, int paperSize, double width, double height)
        {
            _ = paperSize;
            _ = width;
            _ = height;
            if (!string.Equals(templatePath, "C:\\Templates\\Default.drwdot", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            NewDocumentCount++;
            return drawing;
        }

        public bool RunMacro2(string macroPath, string moduleName, string procedureName, int options, int unloadAfterRun)
        {
            _ = moduleName;
            _ = procedureName;
            _ = options;
            _ = unloadAfterRun;
            LastRunMacroPath = macroPath;
            return true;
        }
    }

    private sealed class FakeSourceModel
    {
        public string? LastSavedPath { get; private set; }

        public string GetTitle() => "Part9";

        public string GetPathName() => string.Empty;

        public bool SaveAs3(string path, int saveAsVersion, int options)
        {
            _ = saveAsVersion;
            _ = options;
            LastSavedPath ??= path;
            return true;
        }
    }

    private sealed class FakeDrawingDocument
    {
        private readonly FakeDrawingExtension extension;
        private readonly List<FakeDrawingViewNode> viewNodes;

        public FakeDrawingDocument()
        {
            extension = new FakeDrawingExtension(this);
            viewNodes =
            [
                new FakeDrawingViewNode("Sheet1"),
                new FakeDrawingViewNode("Front"),
            ];
            LinkViews();
        }

        public FakeDrawingView LastView { get; private set; } = new();

        public string? LastViewModelPath { get; private set; }

        public string? LastViewName { get; private set; }

        public new int GetType() => 3;

        public object Extension => extension;

        public string GetTitle() => "Drawing1";

        public string GetPathName() => "C:\\Temp\\Drawing1.slddrw";

        public object? GetFirstView() => viewNodes.Count == 0 ? null : viewNodes[0];

        public object CreateDrawViewFromModelView3(string modelPath, string viewName, double x, double y, double z)
        {
            _ = x;
            _ = y;
            _ = z;
            LastViewModelPath = modelPath;
            LastViewName = viewName;
            LastView = new FakeDrawingView();
            return LastView;
        }

        public object CreateSectionViewAt3(double x, double y, double x1, double y1, double x2, double y2, int sectionType, string label)
        {
            _ = x;
            _ = y;
            _ = x1;
            _ = y1;
            _ = x2;
            _ = y2;
            _ = sectionType;
            var name = string.IsNullOrWhiteSpace(label) ? "SectionView" : label;
            viewNodes.Add(new FakeDrawingViewNode(name));
            LinkViews();
            return new FakeDrawingViewNode(name);
        }

        public void ClearSelection2(bool clearAll)
        {
            _ = clearAll;
        }

        private void LinkViews()
        {
            for (var index = 0; index < viewNodes.Count; index++)
            {
                viewNodes[index].NextView = index + 1 < viewNodes.Count ? viewNodes[index + 1] : null;
            }
        }

        private sealed class FakeDrawingExtension(FakeDrawingDocument drawing)
        {
            public bool SelectByID2(string name, string type, double x, double y, double z, bool append, int mark, object? callout, int option)
            {
                _ = x;
                _ = y;
                _ = z;
                _ = append;
                _ = mark;
                _ = callout;
                _ = option;
                if (!string.Equals(type, "DRAWINGVIEW", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return drawing.viewNodes.Any(view => string.Equals(view.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private sealed class FakeDrawingViewNode(string name)
    {
        public string Name { get; } = name;

        public FakeDrawingViewNode? NextView { get; set; }

        public object? GetNextView() => NextView;
    }

    private sealed class FakeDrawingView
    {
        public double ScaleDecimal { get; set; }
    }

    private sealed class FakeExportModel
    {
        public List<string> SaveAs3Calls { get; } = [];

        public string? InitialSavePath { get; private set; }

        public string GetPathName() => string.Empty;

        public string GetTitle() => "UnsavedPart";

        public bool SaveAs3(string path, int saveAsVersion, int options)
        {
            _ = saveAsVersion;
            _ = options;
            InitialSavePath ??= path;
            SaveAs3Calls.Add(path);
            return true;
        }
    }
}
