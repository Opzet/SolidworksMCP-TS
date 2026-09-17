namespace SolidworksMCP.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class DesignAndPdmResourceTests
{
    [TestMethod]
    public void DesignTableGeneratesVbaCode()
    {
        var resource = new DesignTableResource(
            "dt-1",
            "ParametricBox",
            new Dictionary<string, object?>
            {
                ["tableName"] = "ParametricBox",
                ["parameters"] = new List<Dictionary<string, object?>>
                {
                    new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["name"] = "Length",
                        ["type"] = "dimension",
                        ["dataType"] = "number",
                    },
                },
            });

        var vba = resource.ToVBACode();

        StringAssert.Contains(vba, "Sub CreateDesignTable_ParametricBox()");
        StringAssert.Contains(vba, "swDesignTable.AddParameter \"Length\", \"dimension\"");
    }

    [TestMethod]
    public void PdmGeneratesVbaCode()
    {
        var resource = new PDMResource(
            "pdm-1",
            "Engineering Vault",
            new Dictionary<string, object?>
            {
                ["vaultName"] = "Engineering",
                ["operations"] = new Dictionary<string, object?>
                {
                    ["checkIn"] = new Dictionary<string, object?> { ["enabled"] = true, ["comment"] = "Test check-in" },
                    ["checkOut"] = new Dictionary<string, object?> { ["enabled"] = true, ["getLatestVersion"] = true },
                },
                ["fileStructure"] = new Dictionary<string, object?> { ["rootFolder"] = "C:\\Vault" },
            });

        var vba = resource.ToVBACode();

        StringAssert.Contains(vba, "pdmVault.LoginAuto \"Engineering\"");
        StringAssert.Contains(vba, "pdmFile.LockFile");
        StringAssert.Contains(vba, "pdmFile.UnlockFile");
    }
}
