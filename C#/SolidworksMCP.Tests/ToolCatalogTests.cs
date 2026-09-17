namespace SolidworksMCP.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class ToolCatalogTests
{
    [TestMethod]
    public void CatalogIncludesExpectedTools()
    {
        var tools = ToolCatalog.GetAllTools(new AppConfiguration());
        var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(names.Contains("open_model"));
        Assert.IsTrue(names.Contains("create_extrusion"));
        Assert.IsTrue(names.Contains("generate_vba_script"));
        Assert.IsTrue(names.Contains("get_mass_properties"));
    }

    [TestMethod]
    public async Task GenerateVbaScriptReturnsCode()
    {
        var tool = VbaTools.GetTools().Single(item => item.Name == "generate_vba_script");
        using var document = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["template"] = "create_drawing",
            ["parameters"] = new Dictionary<string, object?>(),
        }));

        var result = await tool.Handler(document.RootElement, new SolidWorksApi(), CancellationToken.None);

        Assert.IsInstanceOfType(result, typeof(string));
        StringAssert.Contains(result.ToString()!, "CreateDrawing");
    }
}
