namespace SolidworksMCP.Tests;

using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class SolidWorksApiCreateSketchTests
{
    [TestMethod]
    public void CreateSketchUsesFeatureByNameWhenPlaneSelectionByIdIsUnavailable()
    {
        FakePlaneFeature plane = new("Front Plane");
        FakeModel model = new(featureByNameResult: plane, features: []);
        SolidWorksApi api = CreateApiWithModel(model);

        Dictionary<string, object?> result = (Dictionary<string, object?>)api.CreateSketch(new Dictionary<string, object?>
        {
            ["plane"] = "Front",
        });

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual(1, plane.SelectCount);
    }

    [TestMethod]
    public void CreateSketchUsesFeatureTraversalWhenFeatureByNameDoesNotResolvePlane()
    {
        FakePlaneFeature otherPlane = new("Front Plane");
        FakePlaneFeature targetPlane = new("Top Plane");
        FakeModel model = new(featureByNameResult: null, features: [otherPlane, targetPlane]);
        SolidWorksApi api = CreateApiWithModel(model);

        Dictionary<string, object?> result = (Dictionary<string, object?>)api.CreateSketch(new Dictionary<string, object?>
        {
            ["plane"] = "Top",
        });

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual(1, targetPlane.SelectCount);
    }

    [TestMethod]
    public void GetSelectionParsesStableSketchHandles()
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["selection"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sketch_name"] = "Sketch1",
                ["sketch_segments"] = new object?[] { "Sketch1:Line1", "Sketch1:Line3" },
                ["sketch_points"] = new object?[] { "Sketch1:Point1" },
            },
        };

        var method = typeof(SolidWorksApi).Assembly.GetType("SolidworksMCP.ToolHelpers")?.GetMethod("GetSelection", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        var selection = (SelectionSpec?)method.Invoke(null, [arguments, "selection"]);
        Assert.IsNotNull(selection);

        CollectionAssert.AreEqual(new[] { "Sketch1:Line1", "Sketch1:Line3" }, selection.SketchSegmentHandles?.ToArray());
        CollectionAssert.AreEqual(new[] { "Sketch1:Point1" }, selection.SketchPointHandles?.ToArray());
        Assert.IsNull(selection.SketchSegments);
        Assert.IsNull(selection.SketchPoints);
        Assert.AreEqual("Sketch1", selection.SketchName);
    }

    [TestMethod]
    public async Task AddRectangleUsesLateBoundSketchManagerAccessor()
    {
        FakeLateBoundSketchManager sketchManager = new();
        FakeLateBoundModel model = new(sketchManager);
        SolidWorksApi api = CreateApiWithModel(model);
        McpToolDefinition tool = SketchTools.GetTools().Single(item => item.Name == "add_rectangle");
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["x1"] = 0,
            ["y1"] = 0,
            ["x2"] = 100,
            ["y2"] = 50,
        }));

        Dictionary<string, object?> result = (Dictionary<string, object?>)(await tool.Handler(document.RootElement, api, CancellationToken.None))!;

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual(1, sketchManager.CreateCornerRectangleCount);
    }

    [TestMethod]
    public async Task AddCircleUsesLateBoundSketchManagerAccessor()
    {
        FakeLateBoundSketchManager sketchManager = new();
        FakeLateBoundModel model = new(sketchManager);
        SolidWorksApi api = CreateApiWithModel(model);
        McpToolDefinition tool = SketchTools.GetTools().Single(item => item.Name == "add_circle");
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["centerX"] = 10,
            ["centerY"] = 15,
            ["radius"] = 5,
        }));

        Dictionary<string, object?> result = (Dictionary<string, object?>)(await tool.Handler(document.RootElement, api, CancellationToken.None))!;

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual(1, sketchManager.CreateCircleCount);
    }

    [TestMethod]
    public async Task ExitSketchSucceedsWhenInsertSketchReturnsVoidAndClosesActiveSketch()
    {
        FakeVoidExitSketchManager sketchManager = new();
        FakeVoidExitModel model = new(sketchManager);
        SolidWorksApi api = CreateApiWithModel(model);
        McpToolDefinition tool = SketchTools.GetTools().Single(item => item.Name == "exit_sketch");
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["rebuild"] = true,
        }));

        Dictionary<string, object?> result = (Dictionary<string, object?>)(await tool.Handler(document.RootElement, api, CancellationToken.None))!;

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual(1, sketchManager.InsertSketchCount);
        Assert.AreEqual(1, model.ForceRebuildCount);
    }

    [TestMethod]
    public async Task RebuildModelSucceedsWhenEditRebuild3ReturnsVoid()
    {
        FakeVoidRebuildModel model = new();
        SolidWorksApi api = CreateApiWithModel(model);
        McpToolDefinition tool = ModelingTools.GetTools().Single(item => item.Name == "rebuild_model");
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["force"] = false,
        }));

        Dictionary<string, object?> result = (Dictionary<string, object?>)(await tool.Handler(document.RootElement, api, CancellationToken.None))!;

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual(1, model.EditRebuild3Count);
    }

    [TestMethod]
    public void CreateExtrudeReturnsNewExtrusionFeatureNameInsteadOfSketch()
    {
        FakeFeatureIdentity sketch = new("Sketch1", "ProfileFeature");
        FakeFeatureIdentity extrusion = new("Boss-Extrude1", "Boss");
        FakeExtrusionModel model = new(sketch, extrusion);
        SolidWorksApi api = CreateApiWithModel(model);

        SolidWorksFeature result = api.CreateExtrude(25);

        Assert.AreEqual("Boss-Extrude1", result.Name);
        Assert.AreEqual(1, model.FeatureManager.FeatureExtrusionCount);
    }

    [TestMethod]
    public void CreateExtrudeUsesFirstFeatureTraversalWhenFeatureCountIsUnavailable()
    {
        FakeFeatureIdentity sketch = new("Sketch1", "ProfileFeature");
        FakeFeatureIdentity extrusion = new("Boss-Extrude2", "Boss");
        FakeFirstFeatureExtrusionModel model = new(sketch, extrusion);
        SolidWorksApi api = CreateApiWithModel(model);

        SolidWorksFeature result = api.CreateExtrude(30);

        Assert.AreEqual("Boss-Extrude2", result.Name);
        Assert.AreEqual(1, model.FeatureManager.FeatureExtrusionCount);
    }

    [TestMethod]
    public void CreateExtrudeUsesNextFeatureWhenReturnedObjectIsNotExtrusion()
    {
        FakeFeatureIdentity sketch = new("Sketch1", "ProfileFeature");
        FakeFeatureIdentity placeholder = new("Origin", "RefOrigin");
        FakeFeatureIdentity extrusion = new("Boss-Extrude3", "Boss");
        FakeReturnedPlaceholderExtrusionModel model = new(sketch, placeholder, extrusion);
        SolidWorksApi api = CreateApiWithModel(model);

        SolidWorksFeature result = api.CreateExtrude(40);

        Assert.AreEqual("Boss-Extrude3", result.Name);
        Assert.AreEqual(1, model.FeatureManager.FeatureExtrusionCount);
    }

    [TestMethod]
    public void CreateExtrudeReselectsMostRecentSketchWhenSelectionIsCleared()
    {
        FakeFeatureIdentity sketch = new("Sketch1", "ProfileFeature");
        FakeFeatureIdentity extrusion = new("Boss-Extrude4", "Boss");
        FakeReselectableExtrusionModel model = new(sketch, extrusion);
        SolidWorksApi api = CreateApiWithModel(model);

        SolidWorksFeature result = api.CreateExtrude(15);

        Assert.AreEqual("Boss-Extrude4", result.Name);
        Assert.AreEqual(1, sketch.SelectCount);
        Assert.AreEqual(1, model.FeatureManager.SelectionCountAtExtrusion);
    }

    [TestMethod]
    public void CreateExtrudeFailureIncludesInvocationAndPreconditionDiagnostics()
    {
        FakeFeatureIdentity sketch = new("Sketch1", "ProfileFeature");
        FakeFailingExtrusionModel model = new(sketch);
        SolidWorksApi api = CreateApiWithModel(model);

        var exception = Assert.ThrowsException<InvalidOperationException>(() => api.CreateExtrude(10));

        StringAssert.Contains(exception.Message, "Preconditions:");
        StringAssert.Contains(exception.Message, "Attempted methods: FeatureExtrusion, FeatureExtrusion3");
        StringAssert.Contains(exception.Message, "Invocation errors:");
        StringAssert.Contains(exception.Message, "selectionCount=1");
        StringAssert.Contains(exception.Message, "activeSketch=Sketch1|ProfileFeature");
        StringAssert.Contains(exception.Message, "Simulated extrusion failure");
    }

    [TestMethod]
    public async Task RebuildModelForceSucceedsWhenOnlyForceRebuild3Exists()
    {
        FakeForceOnlyRebuildModel model = new();
        SolidWorksApi api = CreateApiWithModel(model);
        McpToolDefinition tool = ModelingTools.GetTools().Single(item => item.Name == "rebuild_model");
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["force"] = true,
        }));

        Dictionary<string, object?> result = (Dictionary<string, object?>)(await tool.Handler(document.RootElement, api, CancellationToken.None))!;

        Assert.AreEqual(true, result["success"]);
        Assert.AreEqual(1, model.ForceRebuild3Count);
    }

    [TestMethod]
    public void GetActiveDocumentInfoReportsUnsavedPart()
    {
        FakeDocumentModel model = new("Part42", string.Empty, 1);
        SolidWorksApi api = CreateApiWithModel(model);

        Dictionary<string, object?> result = api.GetActiveDocumentInfo();

        Assert.AreEqual(true, result["hasActiveDocument"]);
        Assert.AreEqual("Part", result["type"]);
        Assert.AreEqual(false, result["isSaved"]);
        Assert.AreEqual("Part42", result["title"]);
    }

    [TestMethod]
    public async Task SaveDocumentUsesRequestedPath()
    {
        FakeSaveDocumentModel model = new();
        SolidWorksApi api = CreateApiWithModel(model);
        McpToolDefinition tool = ModelingTools.GetTools().Single(item => item.Name == "save_document");
        var path = Path.Combine(Path.GetTempPath(), $"swmcp-{Guid.NewGuid():N}", "saved-part.sldprt");
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["path"] = path,
        }));

        var result = await tool.Handler(document.RootElement, api, CancellationToken.None);

        Assert.IsInstanceOfType(result, typeof(string));
        Assert.AreEqual(path, model.SavedPath);
    }

    [TestMethod]
    public void SaveDocumentRejectsPathOutsideConfiguredOutputRoot()
    {
        FakeSaveDocumentModel model = new();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"swmcp-root-{Guid.NewGuid():N}");
        SolidWorksApi api = CreateApiWithModel(model, outputRoot);
        var disallowedPath = Path.Combine(Path.GetTempPath(), $"swmcp-outside-{Guid.NewGuid():N}", "saved-part.sldprt");

        var exception = Assert.ThrowsException<InvalidOperationException>(() => api.SaveDocument(disallowedPath));

        StringAssert.Contains(exception.Message, "outside SW_MCP_OUTPUT_ROOT");
    }

    [TestMethod]
    public async Task SaveActiveDocumentUsesExistingPath()
    {
        var existingPath = Path.Combine(Path.GetTempPath(), $"swmcp-{Guid.NewGuid():N}", "existing-part.sldprt");
        FakeSaveDocumentModel model = new(existingPath);
        SolidWorksApi api = CreateApiWithModel(model);
        McpToolDefinition tool = ModelingTools.GetTools().Single(item => item.Name == "save_active_document");
        using JsonDocument document = JsonDocument.Parse("{}");

        var result = await tool.Handler(document.RootElement, api, CancellationToken.None);

        Assert.IsInstanceOfType(result, typeof(string));
        Assert.AreEqual(existingPath, model.Save3Path);
    }

    [TestMethod]
    public void SaveActiveDocumentRejectsUnsavedDocument()
    {
        FakeSaveDocumentModel model = new();
        SolidWorksApi api = CreateApiWithModel(model);

        var exception = Assert.ThrowsException<InvalidOperationException>(() => api.SaveActiveDocument());

        StringAssert.Contains(exception.Message, "use save_document first");
    }

    private static SolidWorksApi CreateApiWithModel(object model, string? outputRoot = null)
    {
        SolidWorksApi api = new(outputRoot);
        FieldInfo? field = typeof(SolidWorksApi).GetField("currentModel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(api, model);
        return api;
    }

    private sealed class FakeModel(FakePlaneFeature? featureByNameResult, IReadOnlyList<FakePlaneFeature> features)
    {
        public FakeSketchManager SketchManager { get; } = new();

        public FakeExtension Extension { get; } = new();

        public FakeFeatureManager FeatureManager { get; } = new();

        public bool ClearSelection2(bool notify) => notify;

        public object? FeatureByName(string name)
            => featureByNameResult is not null && string.Equals(featureByNameResult.Name, name, StringComparison.OrdinalIgnoreCase)
                ? featureByNameResult
                : null;

        public int GetFeatureCount() => features.Count;

        public object? FeatureByPositionReverse(int index) => index >= 0 && index < features.Count ? features[index] : null;
    }

    private sealed class FakeDocumentModel(string title, string path, int documentType)
    {
        public string GetTitle() => title;

        public string GetPathName() => path;

        public new int GetType() => documentType;
    }

    private sealed class FakeSaveDocumentModel(string? currentPath = null)
    {
        public string SavedPath { get; private set; } = string.Empty;

        public string Save3Path { get; private set; } = string.Empty;

        public string GetTitle() => "UnsavedPart";

        public string GetPathName() => currentPath ?? string.Empty;

        public new int GetType() => 1;

        public bool Save3(int options, int errors, int warnings)
        {
            _ = options;
            _ = errors;
            _ = warnings;
            Save3Path = GetPathName();
            return !string.IsNullOrWhiteSpace(Save3Path);
        }

        public bool SaveAs3(string filePath, int version, int options)
        {
            _ = version;
            _ = options;
            SavedPath = filePath;
            return true;
        }
    }

    private sealed class FakeLateBoundModel(FakeLateBoundSketchManager sketchManager)
    {
        public object get_SketchManager() => sketchManager;

        public bool EditRebuild3() => true;
    }

    private sealed class FakeVoidExitModel(FakeVoidExitSketchManager sketchManager)
    {
        public object get_SketchManager() => sketchManager;

        public object ForceRebuild3(bool topOnly)
        {
            _ = topOnly;
            ForceRebuildCount++;
            return new object();
        }

        public int ForceRebuildCount { get; private set; }
    }

    private sealed class FakeVoidRebuildModel
    {
        public int EditRebuild3Count { get; private set; }

        public void EditRebuild3()
        {
            EditRebuild3Count++;
        }
    }

    private sealed class FakeExtrusionModel
    {
        private readonly List<FakeFeatureIdentity> features;

        public FakeExtrusionModel(FakeFeatureIdentity sketch, FakeFeatureIdentity extrusion)
        {
            features = [sketch];
            FeatureManager = new FakeExtrusionFeatureManager(this, sketch, extrusion);
        }

        public FakeExtrusionFeatureManager FeatureManager { get; }

        public bool ClearSelection2(bool notify) => notify;

        public int GetFeatureCount() => features.Count;

        public object? FeatureByPositionReverse(int index) => index >= 0 && index < features.Count ? features[index] : null;

        public bool EditRebuild3() => true;

        public void AddCreatedExtrusion(FakeFeatureIdentity extrusion)
        {
            if (!features.Contains(extrusion))
            {
                features.Insert(0, extrusion);
            }
        }
    }

    private sealed class FakeFirstFeatureExtrusionModel
    {
        private readonly FakeFeatureIdentity sketch;

        public FakeFirstFeatureExtrusionModel(FakeFeatureIdentity sketch, FakeFeatureIdentity extrusion)
        {
            this.sketch = sketch;
            FeatureManager = new FakeFirstFeatureExtrusionFeatureManager(this, sketch, extrusion);
        }

        public FakeFirstFeatureExtrusionFeatureManager FeatureManager { get; }

        public bool ClearSelection2(bool notify) => notify;

        public object FirstFeature() => sketch;

        public bool EditRebuild3() => true;

        public void AddCreatedExtrusion(FakeFeatureIdentity extrusion)
        {
            extrusion.NextFeature = null;
            sketch.NextFeature = extrusion;
        }
    }

    private sealed class FakeExtrusionFeatureManager(FakeExtrusionModel model, FakeFeatureIdentity returnedFeature, FakeFeatureIdentity createdExtrusion)
    {
        public int FeatureExtrusionCount { get; private set; }

        public object FeatureExtrusion(bool singleDirection, bool reverse, bool bothDirections, int endCondition1, int endCondition2, double depth, int secondDepth, bool draftWhileExtruding, bool draftOutward, bool merge, bool useFeatureScope, int draftAngle1, int draftAngle2)
        {
            _ = singleDirection;
            _ = reverse;
            _ = bothDirections;
            _ = endCondition1;
            _ = endCondition2;
            _ = depth;
            _ = secondDepth;
            _ = draftWhileExtruding;
            _ = draftOutward;
            _ = merge;
            _ = useFeatureScope;
            _ = draftAngle1;
            _ = draftAngle2;
            FeatureExtrusionCount++;
            model.AddCreatedExtrusion(createdExtrusion);
            return returnedFeature;
        }
    }

    private sealed class FakeFirstFeatureExtrusionFeatureManager(FakeFirstFeatureExtrusionModel model, FakeFeatureIdentity returnedFeature, FakeFeatureIdentity createdExtrusion)
    {
        public int FeatureExtrusionCount { get; private set; }

        public object FeatureExtrusion(bool singleDirection, bool reverse, bool bothDirections, int endCondition1, int endCondition2, double depth, int secondDepth, bool draftWhileExtruding, bool draftOutward, bool merge, bool useFeatureScope, int draftAngle1, int draftAngle2)
        {
            _ = singleDirection;
            _ = reverse;
            _ = bothDirections;
            _ = endCondition1;
            _ = endCondition2;
            _ = depth;
            _ = secondDepth;
            _ = draftWhileExtruding;
            _ = draftOutward;
            _ = merge;
            _ = useFeatureScope;
            _ = draftAngle1;
            _ = draftAngle2;
            FeatureExtrusionCount++;
            model.AddCreatedExtrusion(createdExtrusion);
            return returnedFeature;
        }
    }

    private sealed class FakeReturnedPlaceholderExtrusionModel
    {
        private readonly List<FakeFeatureIdentity> features;

        public FakeReturnedPlaceholderExtrusionModel(FakeFeatureIdentity sketch, FakeFeatureIdentity placeholder, FakeFeatureIdentity extrusion)
        {
            features = [sketch, placeholder];
            FeatureManager = new FakeReturnedPlaceholderExtrusionFeatureManager(this, placeholder, extrusion);
        }

        public FakeReturnedPlaceholderExtrusionFeatureManager FeatureManager { get; }

        public bool ClearSelection2(bool notify) => notify;

        public int GetFeatureCount() => features.Count;

        public object? FeatureByPositionReverse(int index) => index >= 0 && index < features.Count ? features[index] : null;

        public bool EditRebuild3() => true;

        public void AddCreatedExtrusion(FakeFeatureIdentity placeholder, FakeFeatureIdentity extrusion)
        {
            placeholder.NextFeature = extrusion;
            if (!features.Contains(extrusion))
            {
                features.Insert(0, extrusion);
            }
        }
    }

    private sealed class FakeReturnedPlaceholderExtrusionFeatureManager(FakeReturnedPlaceholderExtrusionModel model, FakeFeatureIdentity returnedFeature, FakeFeatureIdentity createdExtrusion)
    {
        public int FeatureExtrusionCount { get; private set; }

        public object FeatureExtrusion(bool singleDirection, bool reverse, bool bothDirections, int endCondition1, int endCondition2, double depth, int secondDepth, bool draftWhileExtruding, bool draftOutward, bool merge, bool useFeatureScope, int draftAngle1, int draftAngle2)
        {
            _ = singleDirection;
            _ = reverse;
            _ = bothDirections;
            _ = endCondition1;
            _ = endCondition2;
            _ = depth;
            _ = secondDepth;
            _ = draftWhileExtruding;
            _ = draftOutward;
            _ = merge;
            _ = useFeatureScope;
            _ = draftAngle1;
            _ = draftAngle2;
            FeatureExtrusionCount++;
            model.AddCreatedExtrusion(returnedFeature, createdExtrusion);
            return returnedFeature;
        }
    }

    private sealed class FakeFailingExtrusionModel(FakeFeatureIdentity activeSketch)
    {
        public FakeFailingExtrusionFeatureManager FeatureManager { get; } = new();

        public FakeSelectionManager SelectionManager { get; } = new(1);

        public FakeSketchManagerWithActiveSketch SketchManager { get; } = new(activeSketch);

        public bool ClearSelection2(bool notify) => notify;

        public int GetFeatureCount() => 1;

        public object? FeatureByPositionReverse(int index) => index == 0 ? activeSketch : null;

        public bool EditRebuild3() => true;

        public string GetTitle() => "PartFailure";
    }

    private sealed class FakeReselectableExtrusionModel
    {
        private readonly List<FakeFeatureIdentity> features;

        public FakeReselectableExtrusionModel(FakeFeatureIdentity sketch, FakeFeatureIdentity extrusion)
        {
            features = [sketch];
            SelectionManager = new FakeSelectionManager(0);
            FeatureManager = new FakeReselectableExtrusionFeatureManager(this, sketch, extrusion);
            sketch.OnSelect = () => SelectionManager.SetCount(1);
        }

        public FakeReselectableExtrusionFeatureManager FeatureManager { get; }

        public FakeSelectionManager SelectionManager { get; }

        public FakeSketchManagerWithoutActiveSketch SketchManager { get; } = new();

        public bool ClearSelection2(bool notify)
        {
            SelectionManager.SetCount(0);
            return notify;
        }

        public int GetFeatureCount() => features.Count;

        public object? FeatureByPositionReverse(int index) => index >= 0 && index < features.Count ? features[index] : null;

        public object? FeatureByName(string name)
            => features.FirstOrDefault(feature => string.Equals(feature.Name, name, StringComparison.OrdinalIgnoreCase));

        public bool EditRebuild3() => true;

        public void AddCreatedExtrusion(FakeFeatureIdentity extrusion)
        {
            if (!features.Contains(extrusion))
            {
                features.Insert(0, extrusion);
            }
        }
    }

    private sealed class FakeReselectableExtrusionFeatureManager(FakeReselectableExtrusionModel model, FakeFeatureIdentity returnedFeature, FakeFeatureIdentity createdExtrusion)
    {
        public int FeatureExtrusionCount { get; private set; }

        public int SelectionCountAtExtrusion { get; private set; }

        public object FeatureExtrusion(bool singleDirection, bool reverse, bool bothDirections, int endCondition1, int endCondition2, double depth, int secondDepth, bool draftWhileExtruding, bool draftOutward, bool merge, bool useFeatureScope, int draftAngle1, int draftAngle2)
        {
            _ = singleDirection;
            _ = reverse;
            _ = bothDirections;
            _ = endCondition1;
            _ = endCondition2;
            _ = depth;
            _ = secondDepth;
            _ = draftWhileExtruding;
            _ = draftOutward;
            _ = merge;
            _ = useFeatureScope;
            _ = draftAngle1;
            _ = draftAngle2;
            SelectionCountAtExtrusion = model.SelectionManager.GetSelectedObjectCount2(-1);
            FeatureExtrusionCount++;
            model.AddCreatedExtrusion(createdExtrusion);
            return returnedFeature;
        }
    }

    private sealed class FakeFailingExtrusionFeatureManager
    {
        public object FeatureExtrusion(bool singleDirection, bool reverse, bool bothDirections, int endCondition1, int endCondition2, double depth, int secondDepth, bool draftWhileExtruding, bool draftOutward, bool merge, bool useFeatureScope, int draftAngle1, int draftAngle2)
        {
            _ = singleDirection;
            _ = reverse;
            _ = bothDirections;
            _ = endCondition1;
            _ = endCondition2;
            _ = depth;
            _ = secondDepth;
            _ = draftWhileExtruding;
            _ = draftOutward;
            _ = merge;
            _ = useFeatureScope;
            _ = draftAngle1;
            _ = draftAngle2;
            throw new InvalidOperationException("Simulated extrusion failure");
        }

        public object FeatureExtrusion3(bool singleDirection, bool reverse, bool bothDirections, int endCondition1, int endCondition2, double depth, int secondDepth, bool draftWhileExtruding, bool draftOutward, bool merge, bool useFeatureScope, int draftAngle1, int draftAngle2, bool thinFeature, bool thinWallReverseDirection, bool useAutoSelect, bool useSketchPlane, bool mergeScope, bool featureScope, bool autoSelectComponents, int startCondition, int endConditionReference, bool propagateFeatureToParts)
        {
            _ = singleDirection;
            _ = reverse;
            _ = bothDirections;
            _ = endCondition1;
            _ = endCondition2;
            _ = depth;
            _ = secondDepth;
            _ = draftWhileExtruding;
            _ = draftOutward;
            _ = merge;
            _ = useFeatureScope;
            _ = draftAngle1;
            _ = draftAngle2;
            _ = thinFeature;
            _ = thinWallReverseDirection;
            _ = useAutoSelect;
            _ = useSketchPlane;
            _ = mergeScope;
            _ = featureScope;
            _ = autoSelectComponents;
            _ = startCondition;
            _ = endConditionReference;
            _ = propagateFeatureToParts;
            throw new InvalidOperationException("Simulated extrusion failure");
        }
    }

    private sealed class FakeSketchManagerWithActiveSketch(FakeFeatureIdentity activeSketch)
    {
        public object ActiveSketch => activeSketch;
    }

    private sealed class FakeSketchManagerWithoutActiveSketch
    {
        public object? ActiveSketch => null;
    }

    private sealed class FakeSelectionManager
    {
        private int count;

        public FakeSelectionManager(int count)
        {
            this.count = count;
        }

        public int GetSelectedObjectCount2(int mark) => mark == -1 ? count : 0;

        public int GetSelectedObjectCount() => count;

        public void SetCount(int count)
        {
            this.count = count;
        }
    }

    private sealed class FakeForceOnlyRebuildModel
    {
        public int ForceRebuild3Count { get; private set; }

        public void ForceRebuild3(bool topOnly)
        {
            _ = topOnly;
            ForceRebuild3Count++;
        }
    }

    private sealed class FakeFeatureIdentity(string name, string typeName)
    {
        public string Name { get; } = name;

        public Action? OnSelect { get; set; }

        public int SelectCount { get; private set; }

        public FakeFeatureIdentity? NextFeature { get; set; }

        public string GetTypeName2() => typeName;

        public object? GetNextFeature() => NextFeature;

        public bool Select2(bool append, int mark)
        {
            _ = append;
            _ = mark;
            SelectCount++;
            OnSelect?.Invoke();
            return true;
        }
    }

    private sealed class FakeSketchManager
    {
        public FakeSketch? ActiveSketch { get; private set; }

        public bool InsertSketch(bool updateEditRebuild)
        {
            ActiveSketch = new FakeSketch(updateEditRebuild ? "Sketch1" : "Sketch");
            return true;
        }
    }

    private sealed class FakeLateBoundSketchManager
    {
        public int CreateCircleCount { get; private set; }

        public int CreateCornerRectangleCount { get; private set; }

        public object CreateCircle(double x, double y, double z, double radius)
        {
            _ = x;
            _ = y;
            _ = z;
            _ = radius;
            CreateCircleCount++;
            return new object();
        }

        public object CreateCornerRectangle(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            _ = x1;
            _ = y1;
            _ = z1;
            _ = x2;
            _ = y2;
            _ = z2;
            CreateCornerRectangleCount++;
            return new object();
        }

        public bool InsertSketch(bool updateEditRebuild)
        {
            _ = updateEditRebuild;
            return true;
        }
    }

    private sealed class FakeVoidExitSketchManager
    {
        public FakeSketch? ActiveSketch { get; private set; } = new("Sketch1");

        public int InsertSketchCount { get; private set; }

        public void InsertSketch(bool updateEditRebuild)
        {
            _ = updateEditRebuild;
            InsertSketchCount++;
            ActiveSketch = null;
        }
    }

    private sealed class FakeSketch(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class FakeExtension
    {
        public object SelectByID2(string name, string type, double x, double y, double z, bool append, int mark, object? callout, int options)
            => string.Empty;
    }

    private sealed class FakeFeatureManager
    {
        public object? GetPlane(string name)
            => null;
    }

    private sealed class FakePlaneFeature(string name)
    {
        public string Name { get; } = name;

        public int SelectCount { get; private set; }

        public bool Select2(bool append, int mark)
        {
            _ = append;
            _ = mark;
            SelectCount++;
            return true;
        }
    }
}
