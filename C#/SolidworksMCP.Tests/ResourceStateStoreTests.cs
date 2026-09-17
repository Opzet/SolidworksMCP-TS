namespace SolidworksMCP.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class ResourceStateStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadPreservesState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"solidworks-mcp-state-{Guid.NewGuid():N}.json");
        var store = new ResourceStateStore(path, autoSave: false);

        var state = new ResourceState
        {
            Id = "dt-1",
            Type = "design-table",
            Name = "TestTable",
            Properties = new Dictionary<string, object?> { ["tableName"] = "TestTable" },
            Outputs = new Dictionary<string, object?>(),
            Metadata = new ResourceMetadata(),
            Status = ResourceStatus.Created,
        };

        await store.SetStateAsync(state.Id, state);
        await store.SaveAsync();

        var loaded = new ResourceStateStore(path, autoSave: false);
        await loaded.LoadAsync();

        var restored = loaded.GetState("dt-1");
        Assert.IsNotNull(restored);
        Assert.AreEqual("TestTable", restored!.Name);
        Assert.AreEqual(ResourceStatus.Created, restored.Status);
    }
}
