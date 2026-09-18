namespace SolidworksChatClient.Runtime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ChatRuntimeParsingTests
{
    [TestMethod]
    public void ParseToolCallsFromContent_ParsesFencedFunctionStringObjects()
    {
        const string content = """
```json
{
  "function": "launch_solidworks_and_check_connection",
  "arguments": {}
}

{
  "function": "list_part_templates",
  "arguments": {}
}
```
""";

        var result = ChatRuntime.ParseToolCallsFromContent(content);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("launch_solidworks_and_check_connection", result[0].Function.Name);
        Assert.AreEqual("list_part_templates", result[1].Function.Name);
        Assert.IsNotNull(result[0].Function.Arguments);
        Assert.IsNotNull(result[1].Function.Arguments);
    }

    [TestMethod]
    public void ParseToolCallsFromContent_ParsesNestedFunctionObjects()
    {
        const string content = """
{
  "function": {
    "name": "create_part",
    "arguments": {
      "template": "CAMCO PART.prtdot"
    }
  }
}
""";

        var result = ChatRuntime.ParseToolCallsFromContent(content);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("create_part", result[0].Function.Name);
        Assert.AreEqual("CAMCO PART.prtdot", result[0].Function.Arguments?["template"]?.GetValue<string>());
    }
}
