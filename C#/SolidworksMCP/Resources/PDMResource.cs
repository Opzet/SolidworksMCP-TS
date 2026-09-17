namespace SolidworksMCP;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class PDMResource : SolidWorksResource
{
    [SetsRequiredMembers]
    public PDMResource(string id, string name, Dictionary<string, object?> properties)
        : base(id, name, properties)
    {
    }

    public override string Type => "pdm-configuration";

    public override JsonNode Schema => JsonSchemaBuilder.ObjectWithRequired(
        ["vaultName", "operations", "fileStructure"],
        ("vaultName", JsonSchemaBuilder.String("PDM vault name")),
        ("serverName", JsonSchemaBuilder.String("Optional server name")),
        ("workflowName", JsonSchemaBuilder.String("Optional workflow name")),
        ("operations", JsonSchemaBuilder.Any("Operation configuration")),
        ("fileStructure", JsonSchemaBuilder.Any("File structure configuration")),
        ("metadata", JsonSchemaBuilder.Any("Metadata configuration")),
        ("automation", JsonSchemaBuilder.Any("Automation configuration")));

    public override async ValueTask<object?> ExecuteAsync(SolidWorksApi api, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(api);
        SetStatus(ResourceStatus.Executing);

        try
        {
            var config = GetConfig();
            var results = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["vault"] = config.VaultName,
                ["operations"] = new List<object?>(),
            };

            ConnectToVault(api, config);

            if (config.Operations.CheckOut?.Enabled == true)
            {
                var checkOut = PerformCheckOut(api, config);
                ((List<object?>)results["operations"]!).Add(new Dictionary<string, object?>(checkOut, StringComparer.OrdinalIgnoreCase) { ["type"] = "checkout" });
            }

            if (config.Operations.CheckIn?.Enabled == true)
            {
                var checkIn = PerformCheckIn(api, config);
                ((List<object?>)results["operations"]!).Add(new Dictionary<string, object?>(checkIn, StringComparer.OrdinalIgnoreCase) { ["type"] = "checkin" });
            }

            if (config.Operations.Workflow?.Enabled == true)
            {
                var workflow = ExecuteWorkflow(api, config);
                ((List<object?>)results["operations"]!).Add(new Dictionary<string, object?>(workflow, StringComparer.OrdinalIgnoreCase) { ["type"] = "workflow" });
            }

            if (config.Automation?.Tasks is { Count: > 0 })
            {
                results["automation"] = SetupAutomation(api, config);
            }

            SetStatus(ResourceStatus.Completed);
            SetOutputs(results.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase));
            await Task.CompletedTask;
            return results;
        }
        catch
        {
            SetStatus(ResourceStatus.Failed);
            throw;
        }
    }

    public override IReadOnlyList<string> GetRequiredCapabilities()
    {
        var config = GetConfig();
        var capabilities = new List<string> { "pdm-integration" };

        if (config.Operations.Workflow?.Enabled == true)
        {
            capabilities.Add("pdm-workflow");
        }

        if (config.Automation?.Tasks is { Count: > 0 })
        {
            capabilities.Add("pdm-automation");
        }

        return capabilities;
    }

    public override string ToVBACode()
    {
        var config = GetConfig();
        var lines = new List<string>
        {
            $"' PDM Configuration: {Name}",
            $"' Vault: {config.VaultName}",
            string.Empty,
            "Sub ConfigurePDM()",
            "    Dim pdmVault As EdmVault5",
            "    Dim pdmFile As IEdmFile5",
            "    Dim pdmFolder As IEdmFolder5",
            "    ",
            "    Set pdmVault = New EdmVault5",
            $"    pdmVault.LoginAuto \"{config.VaultName}\", 0",
            "    ",
        };

        if (config.Operations.CheckOut?.Enabled == true)
        {
            lines.Add("    ' Check-out configuration");
            lines.Add("    Dim filePath As String");
            lines.Add($"    filePath = \"{config.Operations.CheckOut.LocalPath ?? "C:\\PDM\\"}\"");
            lines.Add("    ");
            lines.Add("    ' Get file reference");
            lines.Add("    Set pdmFile = pdmVault.GetFileFromPath(filePath, pdmFolder)");
            lines.Add("    ");
            lines.Add("    ' Check out file");
            lines.Add("    If Not pdmFile Is Nothing Then");
            lines.Add("        pdmFile.LockFile pdmFolder.ID, 0");
            lines.Add("    End If");
            lines.Add("    ");
        }

        if (config.Operations.CheckIn?.Enabled == true)
        {
            lines.Add("    ' Check-in configuration");
            lines.Add("    If Not pdmFile Is Nothing Then");
            lines.Add($"        pdmFile.UnlockFile 0, \"{config.Operations.CheckIn.Comment ?? "Auto check-in"}\", 0");
            lines.Add("    End If");
            lines.Add("    ");
        }

        if (config.Operations.Workflow?.Enabled == true && config.Operations.Workflow.Transitions is { Count: > 0 })
        {
            lines.Add("    ' Workflow transitions");
            foreach (var transition in config.Operations.Workflow.Transitions)
            {
                lines.Add($"    ' Transition: {transition.Name}");
                lines.Add("    If Not pdmFile Is Nothing Then");
                lines.Add($"        pdmFile.ChangeState \"{transition.ToState}\", pdmFolder.ID, \"\", 0, 0");
                lines.Add("    End If");
                lines.Add("    ");
            }
        }

        lines.Add("    pdmVault.Logout");
        lines.Add("End Sub");
        return string.Join(Environment.NewLine, lines);
    }

    public override string ToMacroCode()
    {
        var config = GetConfig();
        var actions = new List<object>();

        if (config.Operations.CheckOut?.Enabled == true)
        {
            actions.Add(new { action = "pdm-checkout", parameters = config.Operations.CheckOut });
        }

        if (config.Operations.CheckIn?.Enabled == true)
        {
            actions.Add(new { action = "pdm-checkin", parameters = config.Operations.CheckIn });
        }

        if (config.Operations.Workflow?.Enabled == true)
        {
            actions.Add(new { action = "pdm-workflow", parameters = config.Operations.Workflow });
        }

        return JsonSerializer.Serialize(new
        {
            type = Type,
            name = Name,
            vault = config.VaultName,
            actions,
        }, JsonHelpers.SerializerOptions);
    }

    public void CreateFolderStructure(SolidWorksApi api)
    {
        _ = api;
        var config = GetConfig();
        if (config.FileStructure.FolderStructure is null)
        {
            return;
        }

        foreach (var folder in config.FileStructure.FolderStructure)
        {
            AppLogger.Info($"Creating folder: {folder.Path}");
            if (folder.Permissions is not null)
            {
                AppLogger.Debug($"Setting permissions for: {folder.Path}", folder.Permissions);
            }
        }
    }

    private void ConnectToVault(SolidWorksApi api, PdmConfig config)
    {
        _ = api;
        AppLogger.Info($"Connecting to PDM vault: {config.VaultName}");
    }

    private Dictionary<string, object?> PerformCheckOut(SolidWorksApi api, PdmConfig config)
    {
        _ = api;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["filesCheckedOut"] = new List<string>(),
            ["localPath"] = config.Operations.CheckOut?.LocalPath,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private Dictionary<string, object?> PerformCheckIn(SolidWorksApi api, PdmConfig config)
    {
        _ = api;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = true,
            ["filesCheckedIn"] = new List<string>(),
            ["comment"] = config.Operations.CheckIn?.Comment,
            ["newVersion"] = "1.0.0",
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private Dictionary<string, object?> ExecuteWorkflow(SolidWorksApi api, PdmConfig config)
    {
        _ = api;
        var transitions = new List<object?>();
        if (config.Operations.Workflow?.Transitions is { Count: > 0 })
        {
            foreach (var transition in config.Operations.Workflow.Transitions)
            {
                transitions.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["transition"] = transition.Name,
                    ["from"] = transition.FromState,
                    ["to"] = transition.ToState,
                    ["success"] = true,
                });
            }
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["workflowName"] = config.WorkflowName,
            ["transitions"] = transitions,
        };
    }

    private List<object?> SetupAutomation(SolidWorksApi api, PdmConfig config)
    {
        _ = api;
        var results = new List<object?>();
        if (config.Automation?.Tasks is null)
        {
            return results;
        }

        foreach (var task in config.Automation.Tasks)
        {
            if (task.Enabled)
            {
                results.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["taskName"] = task.Name,
                    ["trigger"] = task.Trigger,
                    ["action"] = task.Action,
                    ["configured"] = true,
                });
            }
        }

        return results;
    }

    private PdmConfig GetConfig()
    {
        return new PdmConfig
        {
            VaultName = GetRequiredString("vaultName"),
            ServerName = GetOptionalString("serverName"),
            WorkflowName = GetOptionalString("workflowName"),
            Operations = ParseOperations(),
            FileStructure = ParseFileStructure(),
            Metadata = ParseMetadata(),
            Automation = ParseAutomation(),
        };
    }

    private string GetRequiredString(string key)
    {
        if (!Properties.TryGetValue(key, out var value) || value is not string text || string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Missing required property: {key}");
        }

        return text;
    }

    private string? GetOptionalString(string key) => Properties.TryGetValue(key, out var value) ? Convert.ToString(value) : null;

    private PdmOperations ParseOperations()
    {
        if (!Properties.TryGetValue("operations", out var value) || value is not Dictionary<string, object?> dictionary)
        {
            return new PdmOperations();
        }

        return new PdmOperations
        {
            CheckIn = ParseCheckIn(dictionary.GetValueOrDefault("checkIn")),
            CheckOut = ParseCheckOut(dictionary.GetValueOrDefault("checkOut")),
            Workflow = ParseWorkflow(dictionary.GetValueOrDefault("workflow")),
            Versioning = ParseVersioning(dictionary.GetValueOrDefault("versioning")),
        };
    }

    private PdmCheckIn? ParseCheckIn(object? value)
    {
        return value is Dictionary<string, object?> dictionary ? new PdmCheckIn
        {
            Enabled = dictionary.TryGetValue("enabled", out var enabled) ? Convert.ToBoolean(enabled) : true,
            Comment = Convert.ToString(dictionary.GetValueOrDefault("comment")),
            KeepCheckedOut = dictionary.TryGetValue("keepCheckedOut", out var keep) && Convert.ToBoolean(keep),
            UpdateReferences = !dictionary.TryGetValue("updateReferences", out var update) || Convert.ToBoolean(update),
        } : null;
    }

    private PdmCheckOut? ParseCheckOut(object? value)
    {
        return value is Dictionary<string, object?> dictionary ? new PdmCheckOut
        {
            Enabled = dictionary.TryGetValue("enabled", out var enabled) ? Convert.ToBoolean(enabled) : true,
            LocalPath = Convert.ToString(dictionary.GetValueOrDefault("localPath")),
            GetLatestVersion = !dictionary.TryGetValue("getLatestVersion", out var latest) || Convert.ToBoolean(latest),
        } : null;
    }

    private PdmWorkflow? ParseWorkflow(object? value)
    {
        return value is Dictionary<string, object?> dictionary ? new PdmWorkflow
        {
            Enabled = dictionary.TryGetValue("enabled", out var enabled) && Convert.ToBoolean(enabled),
            Transitions = ParseWorkflowTransitions(dictionary.GetValueOrDefault("transitions")),
        } : null;
    }

    private PdmVersioning? ParseVersioning(object? value)
    {
        return value is Dictionary<string, object?> dictionary ? new PdmVersioning
        {
            Scheme = Convert.ToString(dictionary.GetValueOrDefault("scheme")) ?? "major",
            AutoIncrement = !dictionary.TryGetValue("autoIncrement", out var auto) || Convert.ToBoolean(auto),
            Format = Convert.ToString(dictionary.GetValueOrDefault("format")),
        } : null;
    }

    private List<PdmWorkflowTransition>? ParseWorkflowTransitions(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return null;
        }

        var transitions = new List<PdmWorkflowTransition>();
        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dictionary)
            {
                transitions.Add(new PdmWorkflowTransition
                {
                    Name = Convert.ToString(dictionary.GetValueOrDefault("name")) ?? string.Empty,
                    FromState = Convert.ToString(dictionary.GetValueOrDefault("fromState")) ?? string.Empty,
                    ToState = Convert.ToString(dictionary.GetValueOrDefault("toState")) ?? string.Empty,
                    Conditions = dictionary.TryGetValue("conditions", out var conditions) && conditions is IEnumerable<object?> conditionItems
                        ? conditionItems.Select(Convert.ToString).Where(text => !string.IsNullOrWhiteSpace(text)).Cast<string>().ToList()
                        : null,
                });
            }
        }

        return transitions;
    }

    private PdmFileStructure ParseFileStructure()
    {
        if (!Properties.TryGetValue("fileStructure", out var value) || value is not Dictionary<string, object?> dictionary)
        {
            return new PdmFileStructure { RootFolder = string.Empty };
        }

        return new PdmFileStructure
        {
            RootFolder = Convert.ToString(dictionary.GetValueOrDefault("rootFolder")) ?? string.Empty,
            ProjectTemplate = Convert.ToString(dictionary.GetValueOrDefault("projectTemplate")),
            NamingConvention = ParseNamingConvention(dictionary.GetValueOrDefault("namingConvention")),
            FolderStructure = ParseFolderStructure(dictionary.GetValueOrDefault("folderStructure")),
        };
    }

    private PdmNamingConvention? ParseNamingConvention(object? value)
    {
        return value is Dictionary<string, object?> dictionary ? new PdmNamingConvention
        {
            Pattern = Convert.ToString(dictionary.GetValueOrDefault("pattern")) ?? string.Empty,
            Variables = ParseNamingVariables(dictionary.GetValueOrDefault("variables")),
        } : null;
    }

    private List<PdmNamingVariable>? ParseNamingVariables(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return null;
        }

        var variables = new List<PdmNamingVariable>();
        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dictionary)
            {
                variables.Add(new PdmNamingVariable
                {
                    Name = Convert.ToString(dictionary.GetValueOrDefault("name")) ?? string.Empty,
                    Type = Convert.ToString(dictionary.GetValueOrDefault("type")) ?? string.Empty,
                    Format = Convert.ToString(dictionary.GetValueOrDefault("format")),
                });
            }
        }

        return variables;
    }

    private List<PdmFolderDefinition>? ParseFolderStructure(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return null;
        }

        var folders = new List<PdmFolderDefinition>();
        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dictionary)
            {
                folders.Add(new PdmFolderDefinition
                {
                    Path = Convert.ToString(dictionary.GetValueOrDefault("path")) ?? string.Empty,
                    Permissions = ParseFolderPermissions(dictionary.GetValueOrDefault("permissions")),
                });
            }
        }

        return folders;
    }

    private PdmFolderPermissions? ParseFolderPermissions(object? value)
    {
        return value is Dictionary<string, object?> dictionary ? new PdmFolderPermissions
        {
            Read = ToStringList(dictionary.GetValueOrDefault("read")),
            Write = ToStringList(dictionary.GetValueOrDefault("write")),
            Delete = ToStringList(dictionary.GetValueOrDefault("delete")),
        } : null;
    }

    private PdmMetadata? ParseMetadata()
    {
        if (!Properties.TryGetValue("metadata", out var value) || value is not Dictionary<string, object?> dictionary)
        {
            return null;
        }

        return new PdmMetadata
        {
            CustomProperties = ParseCustomProperties(dictionary.GetValueOrDefault("customProperties")),
            DataCards = ParseDataCards(dictionary.GetValueOrDefault("dataCards")),
        };
    }

    private List<string> ToStringList(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return [];
        }

        return items.Select(Convert.ToString).Where(text => !string.IsNullOrWhiteSpace(text)).Cast<string>().ToList();
    }

    private List<PdmCustomProperty>? ParseCustomProperties(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return null;
        }

        var properties = new List<PdmCustomProperty>();
        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dictionary)
            {
                properties.Add(new PdmCustomProperty
                {
                    Name = Convert.ToString(dictionary.GetValueOrDefault("name")) ?? string.Empty,
                    Type = Convert.ToString(dictionary.GetValueOrDefault("type")) ?? string.Empty,
                    Required = dictionary.TryGetValue("required", out var required) && Convert.ToBoolean(required),
                    DefaultValue = dictionary.GetValueOrDefault("defaultValue"),
                    ListValues = ToStringList(dictionary.GetValueOrDefault("listValues")),
                });
            }
        }

        return properties;
    }

    private List<PdmDataCard>? ParseDataCards(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return null;
        }

        var cards = new List<PdmDataCard>();
        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dictionary)
            {
                cards.Add(new PdmDataCard
                {
                    Name = Convert.ToString(dictionary.GetValueOrDefault("name")) ?? string.Empty,
                    FileType = Convert.ToString(dictionary.GetValueOrDefault("fileType")) ?? string.Empty,
                    Controls = ParseDataCardControls(dictionary.GetValueOrDefault("controls")),
                });
            }
        }

        return cards;
    }

    private List<PdmControl> ParseDataCardControls(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return [];
        }

        var controls = new List<PdmControl>();
        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dictionary)
            {
                controls.Add(new PdmControl
                {
                    Type = Convert.ToString(dictionary.GetValueOrDefault("type")) ?? string.Empty,
                    Variable = Convert.ToString(dictionary.GetValueOrDefault("variable")) ?? string.Empty,
                    Position = ParseControlPosition(dictionary.GetValueOrDefault("position")) ?? new PdmPosition(),
                });
            }
        }

        return controls;
    }

    private PdmPosition? ParseControlPosition(object? value)
    {
        return value is Dictionary<string, object?> dictionary ? new PdmPosition
        {
            X = Convert.ToInt32(dictionary.GetValueOrDefault("x")),
            Y = Convert.ToInt32(dictionary.GetValueOrDefault("y")),
            Width = Convert.ToInt32(dictionary.GetValueOrDefault("width")),
            Height = Convert.ToInt32(dictionary.GetValueOrDefault("height")),
        } : null;
    }

    private PdmAutomation? ParseAutomation()
    {
        if (!Properties.TryGetValue("automation", out var value) || value is not Dictionary<string, object?> dictionary)
        {
            return null;
        }

        return new PdmAutomation
        {
            Tasks = ParseAutomationTasks(dictionary.GetValueOrDefault("tasks")),
            Notifications = ParseNotifications(dictionary.GetValueOrDefault("notifications")),
        };
    }

    private List<PdmTask>? ParseAutomationTasks(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return null;
        }

        var tasks = new List<PdmTask>();
        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dictionary)
            {
                tasks.Add(new PdmTask
                {
                    Name = Convert.ToString(dictionary.GetValueOrDefault("name")) ?? string.Empty,
                    Trigger = Convert.ToString(dictionary.GetValueOrDefault("trigger")) ?? string.Empty,
                    Action = Convert.ToString(dictionary.GetValueOrDefault("action")) ?? string.Empty,
                    Parameters = dictionary.TryGetValue("parameters", out var parameters) && parameters is Dictionary<string, object?> parameterDictionary
                        ? new Dictionary<string, object?>(parameterDictionary, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                    Enabled = !dictionary.TryGetValue("enabled", out var enabled) || Convert.ToBoolean(enabled),
                });
            }
        }

        return tasks;
    }

    private List<PdmNotification>? ParseNotifications(object? value)
    {
        if (value is not IEnumerable<object?> items)
        {
            return null;
        }

        var notifications = new List<PdmNotification>();
        foreach (var item in items)
        {
            if (item is Dictionary<string, object?> dictionary)
            {
                notifications.Add(new PdmNotification
                {
                    Event = Convert.ToString(dictionary.GetValueOrDefault("event")) ?? string.Empty,
                    Recipients = ToStringList(dictionary.GetValueOrDefault("recipients")),
                    Template = Convert.ToString(dictionary.GetValueOrDefault("template")) ?? string.Empty,
                });
            }
        }

        return notifications;
    }

    private sealed record PdmConfig
    {
        public required string VaultName { get; init; }

        public string? ServerName { get; init; }

        public string? WorkflowName { get; init; }

        public required PdmOperations Operations { get; init; }

        public required PdmFileStructure FileStructure { get; init; }

        public PdmMetadata? Metadata { get; init; }

        public PdmAutomation? Automation { get; init; }
    }

    private sealed record PdmOperations
    {
        public PdmCheckIn? CheckIn { get; init; }

        public PdmCheckOut? CheckOut { get; init; }

        public PdmWorkflow? Workflow { get; init; }

        public PdmVersioning? Versioning { get; init; }
    }

    private sealed record PdmCheckIn
    {
        public bool Enabled { get; init; } = true;

        public string? Comment { get; init; }

        public bool KeepCheckedOut { get; init; }

        public bool UpdateReferences { get; init; } = true;
    }

    private sealed record PdmCheckOut
    {
        public bool Enabled { get; init; } = true;

        public string? LocalPath { get; init; }

        public bool GetLatestVersion { get; init; } = true;
    }

    private sealed record PdmWorkflow
    {
        public bool Enabled { get; init; }

        public List<PdmWorkflowTransition>? Transitions { get; init; }
    }

    private sealed record PdmWorkflowTransition
    {
        public required string Name { get; init; }

        public required string FromState { get; init; }

        public required string ToState { get; init; }

        public List<string>? Conditions { get; init; }
    }

    private sealed record PdmVersioning
    {
        public required string Scheme { get; init; }

        public bool AutoIncrement { get; init; } = true;

        public string? Format { get; init; }
    }

    private sealed record PdmFileStructure
    {
        public required string RootFolder { get; init; }

        public string? ProjectTemplate { get; init; }

        public PdmNamingConvention? NamingConvention { get; init; }

        public List<PdmFolderDefinition>? FolderStructure { get; init; }
    }

    private sealed record PdmNamingConvention
    {
        public required string Pattern { get; init; }

        public List<PdmNamingVariable>? Variables { get; init; }
    }

    private sealed record PdmNamingVariable
    {
        public required string Name { get; init; }

        public required string Type { get; init; }

        public string? Format { get; init; }
    }

    private sealed record PdmFolderDefinition
    {
        public required string Path { get; init; }

        public PdmFolderPermissions? Permissions { get; init; }
    }

    private sealed record PdmFolderPermissions
    {
        public List<string> Read { get; init; } = new();

        public List<string> Write { get; init; } = new();

        public List<string> Delete { get; init; } = new();
    }

    private sealed record PdmMetadata
    {
        public List<PdmCustomProperty>? CustomProperties { get; init; }

        public List<PdmDataCard>? DataCards { get; init; }
    }

    private sealed record PdmCustomProperty
    {
        public required string Name { get; init; }

        public required string Type { get; init; }

        public bool Required { get; init; }

        public object? DefaultValue { get; init; }

        public List<string>? ListValues { get; init; }
    }

    private sealed record PdmDataCard
    {
        public required string Name { get; init; }

        public required string FileType { get; init; }

        public List<PdmControl> Controls { get; init; } = new();
    }

    private sealed record PdmControl
    {
        public required string Type { get; init; }

        public required string Variable { get; init; }

        public required PdmPosition Position { get; init; }
    }

    private sealed record PdmPosition
    {
        public int X { get; init; }

        public int Y { get; init; }

        public int Width { get; init; }

        public int Height { get; init; }
    }

    private sealed record PdmAutomation
    {
        public List<PdmTask>? Tasks { get; init; }

        public List<PdmNotification>? Notifications { get; init; }
    }

    private sealed record PdmTask
    {
        public required string Name { get; init; }

        public required string Trigger { get; init; }

        public required string Action { get; init; }

        public Dictionary<string, object?> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Enabled { get; init; } = true;
    }

    private sealed record PdmNotification
    {
        public required string Event { get; init; }

        public List<string> Recipients { get; init; } = new();

        public required string Template { get; init; }
    }
}
