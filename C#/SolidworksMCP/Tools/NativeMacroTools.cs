namespace SolidworksMCP;

using System.Text.Json;

public static class NativeMacroTools
{
    public static IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition("start_native_macro_recording", "Start recording a macro using SolidWorks native VBA recorder", JsonSchemaBuilder.ObjectWithRequired(["macroPath"], ("macroPath", JsonSchemaBuilder.String()), ("pauseRecording", JsonSchemaBuilder.Boolean(defaultValue: false)), ("recordViewCommands", JsonSchemaBuilder.Boolean(defaultValue: false)), ("recordFeatureManager", JsonSchemaBuilder.Boolean(defaultValue: true)), ("recordSelections", JsonSchemaBuilder.Boolean(defaultValue: true))), HandleStart),
        new McpToolDefinition("stop_native_macro_recording", "Stop the current native macro recording and save", JsonSchemaBuilder.Object(("openInEditor", JsonSchemaBuilder.Boolean(defaultValue: false)), ("runMacro", JsonSchemaBuilder.Boolean(defaultValue: false))), HandleStop),
        new McpToolDefinition("pause_resume_macro_recording", "Pause or resume the current macro recording", JsonSchemaBuilder.ObjectWithRequired(["action"], ("action", JsonSchemaBuilder.Enum(["pause", "resume"]))), HandlePauseResume),
        new McpToolDefinition("run_macro", "Run a SolidWorks macro file", JsonSchemaBuilder.ObjectWithRequired(["macroPath"], ("macroPath", JsonSchemaBuilder.String()), ("moduleName", JsonSchemaBuilder.String("Module name", "main")), ("procedureName", JsonSchemaBuilder.String("Procedure name", "main")), ("arguments", JsonSchemaBuilder.Array(JsonSchemaBuilder.Any())), ("unloadAfterRun", JsonSchemaBuilder.Boolean(defaultValue: true))), HandleRunMacro),
    ];

    private static ValueTask<object?> HandleStart(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var swApp = api.GetApp() ?? throw new InvalidOperationException("SolidWorks application not connected");
            var macroPath = EnsureSwpExtension(ToolHelpers.GetString(args, "macroPath"));
            Directory.CreateDirectory(Path.GetDirectoryName(macroPath)!);
            SetToggle(swApp, 197, ToolHelpers.GetBool(args, "recordViewCommands"));
            SetToggle(swApp, 198, ToolHelpers.GetBool(args, "recordFeatureManager", true));
            SetToggle(swApp, 199, ToolHelpers.GetBool(args, "recordSelections", true));
            if (InvokeBool(swApp, "RecordMacro", macroPath) != true)
            {
                throw new InvalidOperationException("Failed to start macro recording");
            }

            if (ToolHelpers.GetBool(args, "pauseRecording"))
            {
                Invoke(swApp, "PauseMacroRecording");
            }

            return ValueTask.FromResult<object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["message"] = "Native macro recording started",
                ["macroPath"] = macroPath,
                ["status"] = ToolHelpers.GetBool(args, "pauseRecording") ? "paused" : "recording",
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to start native macro recording: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleStop(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var swApp = api.GetApp() ?? throw new InvalidOperationException("SolidWorks application not connected");
            Invoke(swApp, "StopMacroRecording");
            var lastMacroPath = Convert.ToString(Invoke(swApp, "GetUserPreferenceStringValue", 69)) ?? string.Empty;
            if (ToolHelpers.GetBool(args, "openInEditor"))
            {
                Invoke(swApp, "EditMacro", lastMacroPath);
            }

            if (ToolHelpers.GetBool(args, "runMacro"))
            {
                Invoke(swApp, "RunMacro2", lastMacroPath, "main", "main", 1);
            }

            return ValueTask.FromResult<object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["message"] = "Native macro recording stopped and saved",
                ["macroPath"] = lastMacroPath,
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to stop native macro recording: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandlePauseResume(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var swApp = api.GetApp() ?? throw new InvalidOperationException("SolidWorks application not connected");
            var action = ToolHelpers.GetString(args, "action");
            if (action == "pause")
            {
                Invoke(swApp, "PauseMacroRecording");
            }
            else
            {
                Invoke(swApp, "ResumeMacroRecording");
            }

            return ValueTask.FromResult<object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["message"] = $"Macro recording {action}d",
                ["status"] = action == "pause" ? "paused" : "recording",
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to pause/resume macro recording: {ex.Message}"));
        }
    }

    private static ValueTask<object?> HandleRunMacro(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            var args = ToolHelpers.ToArguments(arguments);
            var macroPath = ToolHelpers.GetString(args, "macroPath");
            var moduleName = ToolHelpers.GetString(args, "moduleName", "main");
            var procedureName = ToolHelpers.GetString(args, "procedureName", "main");
            var result = api.RunMacro(macroPath, moduleName, procedureName);
            return ValueTask.FromResult<object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["success"] = true,
                ["result"] = result,
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult<object?>(ToolHelpers.Failure($"Failed to run macro: {ex.Message}"));
        }
    }

    private static string EnsureSwpExtension(string macroPath)
    {
        if (macroPath.EndsWith(".swp", StringComparison.OrdinalIgnoreCase))
        {
            return macroPath;
        }

        return Path.ChangeExtension(macroPath, ".swp") ?? macroPath + ".swp";
    }

    private static object? Invoke(object target, string method, params object?[] args)
        => target.GetType().GetMethod(method)?.Invoke(target, args);

    private static bool? InvokeBool(object target, string method, params object?[] args)
        => Invoke(target, method, args) as bool?;

    private static void SetToggle(object target, int id, bool value)
        => Invoke(target, "SetUserPreferenceToggle", id, value);
}
