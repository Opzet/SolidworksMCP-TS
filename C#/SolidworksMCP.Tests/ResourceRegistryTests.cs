namespace SolidworksMCP.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class ResourceRegistryTests
{
    [TestInitialize]
    public void Initialize()
    {
        ResourceRegistrySingleton.Instance.Clear();
        ResourceFactory.RegisterDefaults(new AppConfiguration());
    }

    [TestMethod]
    public void RegisterDefaultsAddsDesignTableResource()
    {
        var types = ResourceRegistrySingleton.Instance.GetAllTypes();

        CollectionAssert.Contains(types.ToList(), "design-table");
    }

    [TestMethod]
    public void CreateResourceReturnsDesignTableResource()
    {
        var resource = ResourceRegistrySingleton.Instance.CreateResource(
            "design-table",
            "dt-1",
            "TestTable",
            new Dictionary<string, object?>
            {
                ["tableName"] = "TestTable",
                ["parameters"] = new List<Dictionary<string, object?>>(),
            });

        Assert.IsInstanceOfType(resource, typeof(DesignTableResource));
        Assert.AreEqual("dt-1", resource.Id);
        Assert.AreEqual("TestTable", resource.Name);
    }
}
