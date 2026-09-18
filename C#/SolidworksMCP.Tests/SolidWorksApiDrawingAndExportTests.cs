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
        public FakeDrawingView LastView { get; private set; } = new();

        public string? LastViewModelPath { get; private set; }

        public string? LastViewName { get; private set; }

        public string GetTitle() => "Drawing1";

        public string GetPathName() => string.Empty;

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
