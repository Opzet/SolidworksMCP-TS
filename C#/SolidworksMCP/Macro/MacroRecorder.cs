namespace SolidworksMCP;

using System.Text;

public sealed class MacroRecorder
{
    private MacroRecording? currentRecording;
    private readonly Dictionary<string, MacroRecording> recordings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MacroExecution> executions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<MacroAction, ValueTask<object?>>> actionHandlers = new(StringComparer.OrdinalIgnoreCase);

    public string StartRecording(string name, string description = "")
    {
        if (currentRecording is not null)
        {
            throw new InvalidOperationException("A recording is already in progress");
        }

        var id = Guid.NewGuid().ToString();
        currentRecording = new MacroRecording
        {
            Id = id,
            Name = name,
            Description = description,
            StartTime = DateTimeOffset.UtcNow.ToString("O"),
            Actions = new List<MacroAction>(),
            Metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["createdBy"] = "solidworks-mcp",
                ["version"] = "1.0.0",
                ["tags"] = new List<string>(),
            },
        };

        return id;
    }

    public MacroRecording? StopRecording()
    {
        if (currentRecording is null)
        {
            return null;
        }

        currentRecording.EndTime = DateTimeOffset.UtcNow.ToString("O");
        var recording = currentRecording;
        recordings[recording.Id] = recording;
        currentRecording = null;
        return recording;
    }

    public void RecordAction(string type, string name, Dictionary<string, object?> parameters)
    {
        if (currentRecording is null)
        {
            throw new InvalidOperationException("No recording in progress");
        }

        currentRecording.Actions.Add(new MacroAction
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Name = name,
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            Parameters = parameters,
        });
    }

    public MacroRecording? GetRecording(string id)
        => recordings.TryGetValue(id, out var recording) ? recording : null;

    public IReadOnlyList<MacroRecording> GetAllRecordings() => recordings.Values.ToList();

    public bool DeleteRecording(string id) => recordings.Remove(id);

    public void RegisterActionHandler(string type, Func<MacroAction, ValueTask<object?>> handler)
    {
        actionHandlers[type] = handler;
    }

    public async Task<MacroExecution> ExecuteMacroAsync(string macroId, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        if (!recordings.TryGetValue(macroId, out var recording))
        {
            throw new InvalidOperationException($"Macro '{macroId}' not found");
        }

        var execution = new MacroExecution
        {
            Id = Guid.NewGuid().ToString(),
            MacroId = macroId,
            StartTime = DateTimeOffset.UtcNow.ToString("O"),
            Status = "running",
            Logs = new List<MacroLog>(),
        };

        executions[execution.Id] = execution;

        try
        {
            var results = new List<object?>();
            foreach (var action in recording.Actions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!actionHandlers.TryGetValue(action.Type, out var handler))
                {
                    throw new InvalidOperationException($"No handler registered for action type '{action.Type}'");
                }

                AddLog(execution, "info", $"Executing action: {action.Name}");
                var mergedParameters = new Dictionary<string, object?>(action.Parameters, StringComparer.OrdinalIgnoreCase);
                if (parameters is not null)
                {
                    foreach (var pair in parameters)
                    {
                        mergedParameters[pair.Key] = pair.Value;
                    }
                }

                var result = await handler(action with { Parameters = mergedParameters }).ConfigureAwait(false);
                results.Add(result);
                AddLog(execution, "debug", $"Action completed: {action.Name}", result);
            }

            execution.Status = "completed";
            execution.EndTime = DateTimeOffset.UtcNow.ToString("O");
            execution.Result = results;
        }
        catch (Exception ex)
        {
            execution.Status = "failed";
            execution.EndTime = DateTimeOffset.UtcNow.ToString("O");
            execution.Error = ex.Message;
            AddLog(execution, "error", "Macro execution failed", ex.Message);
        }

        return execution;
    }

    public MacroExecution? GetExecution(string id)
        => executions.TryGetValue(id, out var execution) ? execution : null;

    public IReadOnlyList<MacroExecution> GetMacroExecutions(string macroId)
        => executions.Values.Where(execution => string.Equals(execution.MacroId, macroId, StringComparison.OrdinalIgnoreCase)).ToList();

    public string ExportToVba(string macroId)
    {
        if (!recordings.TryGetValue(macroId, out var recording))
        {
            throw new InvalidOperationException($"Macro '{macroId}' not found");
        }

        var lines = new List<string>
        {
            $"' Macro: {recording.Name}",
            $"' Description: {recording.Description}",
            $"' Generated: {DateTimeOffset.UtcNow:O}",
            "' By: SolidWorks MCP Server",
            string.Empty,
            $"Sub {SanitizeName(recording.Name)}()",
            "    Dim swApp As SldWorks.SldWorks",
            "    Dim swModel As SldWorks.ModelDoc2",
            "    ",
            "    Set swApp = Application.SldWorks",
            "    Set swModel = swApp.ActiveDoc",
            "    ",
            "    If swModel Is Nothing Then",
            "        MsgBox \"No active document found\"",
            "        Exit Sub",
            "    End If",
            "    ",
        };

        foreach (var action in recording.Actions)
        {
            var vbaCode = ActionToVba(action);
            if (string.IsNullOrWhiteSpace(vbaCode))
            {
                continue;
            }

            lines.Add($"    ' {action.Name}");
            lines.AddRange(vbaCode.Split('\n').Select(line => $"    {line}"));
            lines.Add(string.Empty);
        }

        lines.Add("End Sub");
        return string.Join(Environment.NewLine, lines);
    }

    public void Clear()
    {
        currentRecording = null;
        recordings.Clear();
        executions.Clear();
    }

    private static void AddLog(MacroExecution execution, string level, string message, object? data = null)
    {
        execution.Logs.Add(new MacroLog
        {
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            Level = level,
            Message = message,
            Data = data,
        });
    }

    private static string ActionToVba(MacroAction action)
    {
        return action.Type switch
        {
            "create-sketch" => $"swModel.CreateSketch \"{GetString(action.Parameters, "plane", "Front")}\"",
            "add-line" => $"swModel.CreateLine2 {GetValue(action.Parameters, "x1")}, {GetValue(action.Parameters, "y1")}, {GetValue(action.Parameters, "z1")}, {GetValue(action.Parameters, "x2")}, {GetValue(action.Parameters, "y2")}, {GetValue(action.Parameters, "z2")}",
            "add-circle" => $"swModel.CreateCircle2 {GetValue(action.Parameters, "centerX")}, {GetValue(action.Parameters, "centerY")}, {GetValue(action.Parameters, "centerZ")}, {GetValue(action.Parameters, "radius")}",
            "extrude" => $"swModel.FeatureManager.FeatureExtrusion3 True, False, False, 0, 0, {GetValue(action.Parameters, "depth")}, 0, False, False, False, False, 0, 0, False, False, False, False, True, True, True, 0, 0, False",
            _ => $"' Unsupported action: {action.Type}",
        };
    }

    private static string GetString(Dictionary<string, object?> parameters, string name, string defaultValue)
        => parameters.TryGetValue(name, out var value) ? Convert.ToString(value) ?? defaultValue : defaultValue;

    private static string GetValue(Dictionary<string, object?> parameters, string name)
        => parameters.TryGetValue(name, out var value) ? Convert.ToString(value) ?? "0" : "0";

    private static string SanitizeName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return builder.ToString();
    }
}
