namespace SolidworksMCP.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class MacroRecorderTests
{
    [TestMethod]
    public void StartAndStopRecordingCreatesRecording()
    {
        var recorder = new MacroRecorder();

        var id = recorder.StartRecording("TestMacro", "Test description");
        recorder.RecordAction("create-sketch", "Create Sketch", new Dictionary<string, object?> { ["plane"] = "Front" });
        var recording = recorder.StopRecording();

        Assert.IsNotNull(recording);
        Assert.AreEqual(id, recording!.Id);
        Assert.AreEqual("TestMacro", recording.Name);
        Assert.AreEqual(1, recording.Actions.Count);
    }

    [TestMethod]
    public void ExportToVbaIncludesMacroNameAndAction()
    {
        var recorder = new MacroRecorder();

        var id = recorder.StartRecording("ExportTest");
        recorder.RecordAction("create-sketch", "Create Sketch", new Dictionary<string, object?> { ["plane"] = "Front" });
        recorder.StopRecording();

        var vba = recorder.ExportToVba(id);

        StringAssert.Contains(vba, "Sub ExportTest()");
        StringAssert.Contains(vba, "Create Sketch");
    }
}
