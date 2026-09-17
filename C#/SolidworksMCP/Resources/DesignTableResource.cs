namespace SolidworksMCP;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed record DesignTableParameter
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string DataType { get; init; }

    public object? DefaultValue { get; init; }

    public string? SqlColumn { get; init; }

    public string? Formula { get; init; }
}

public sealed record DesignTableConfigurationRow
{
    public required string Name { get; init; }

    public required Dictionary<string, object?> Values { get; init; }

    public bool Active { get; init; }
}

public sealed record DesignTableDataSource
{
    public required string Type { get; init; }

    public string? ConnectionString { get; init; }

    public string? Query { get; init; }

    public string? FilePath { get; init; }

    public string? ApiEndpoint { get; init; }

    public int? RefreshInterval { get; init; }
}

public sealed record DesignTableValidation
{
    public bool Enabled { get; init; } = true;

    public IReadOnlyList<DesignTableValidationRule>? Rules { get; init; }
}

public sealed record DesignTableValidationRule
{
    public required string Parameter { get; init; }

    public required string Rule { get; init; }

    public required string Message { get; init; }
}

public sealed record DesignTableConfig
{
    public required string TableName { get; init; }

    public string? FileName { get; init; }

    public required List<DesignTableParameter> Parameters { get; init; }

    public List<DesignTableConfigurationRow>? Configurations { get; init; }

    public DesignTableDataSource? DataSource { get; init; }

    public bool AutoUpdate { get; init; }

    public DesignTableValidation? Validation { get; init; }
}

public sealed class DesignTableResource : SolidWorksResource
{
    [SetsRequiredMembers]
    public DesignTableResource(string id, string name, Dictionary<string, object?> properties)
        : base(id, name, properties)
    {
    }

    public override string Type => "design-table";

    public override JsonNode Schema => JsonSchemaBuilder.ObjectWithRequired(
        ["tableName", "parameters"],
        ("tableName", JsonSchemaBuilder.String("Design table name")),
        ("fileName", JsonSchemaBuilder.String("Optional file name")),
        ("parameters", JsonSchemaBuilder.Array(JsonSchemaBuilder.Any("Parameter definition"))),
        ("configurations", JsonSchemaBuilder.Array(JsonSchemaBuilder.Any("Configuration row"))),
        ("dataSource", JsonSchemaBuilder.Any("Data source settings")),
        ("autoUpdate", JsonSchemaBuilder.Boolean("Automatically update the table", false)),
        ("validation", JsonSchemaBuilder.Any("Validation settings")));

    public override async ValueTask<object?> ExecuteAsync(SolidWorksApi api, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(api);
        SetStatus(ResourceStatus.Executing);

        try
        {
            var config = GetConfig();

            if (config.DataSource?.Type.Equals("sql", StringComparison.OrdinalIgnoreCase) == true)
            {
                await LoadFromSqlAsync(config, cancellationToken).ConfigureAwait(false);
            }

            var result = UpdateDesignTable(api, config);
            var resultMap = result as Dictionary<string, object?> ?? throw new InvalidOperationException("Design table update did not return a dictionary result.");

            SetStatus(ResourceStatus.Completed);
            SetOutputs(new Dictionary<string, object?>
            {
                ["tableId"] = resultMap["tableId"],
                ["configurationsCreated"] = resultMap["configurations"],
                ["parametersUpdated"] = resultMap["parameters"],
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            });

            return resultMap;
        }
        catch
        {
            SetStatus(ResourceStatus.Failed);
            throw;
        }
    }

    public async Task RefreshAsync(SolidWorksApi api, CancellationToken cancellationToken = default)
    {
        var config = GetConfig();
        if (config.DataSource?.Type.Equals("sql", StringComparison.OrdinalIgnoreCase) == true)
        {
            await LoadFromSqlAsync(config, cancellationToken).ConfigureAwait(false);
            UpdateDesignTable(api, config);
        }
    }

    public ValidationResult ValidateConfiguration()
    {
        var config = GetConfig();
        var warnings = new List<ValidationIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in config.Parameters)
        {
            if (!seen.Add(parameter.Name))
            {
                warnings.Add(new ValidationIssue($"parameters.{parameter.Name}", $"Duplicate parameter name: {parameter.Name}"));
            }
        }

        if (config.DataSource?.Type.Equals("sql", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (string.IsNullOrWhiteSpace(config.DataSource.ConnectionString))
            {
                warnings.Add(new ValidationIssue("dataSource.connectionString", "SQL connection string is required for SQL data source"));
            }

            if (string.IsNullOrWhiteSpace(config.DataSource.Query))
            {
                warnings.Add(new ValidationIssue("dataSource.query", "SQL query is required for SQL data source"));
            }
        }

        return warnings.Count > 0 ? new ValidationResult(false, Array.Empty<ValidationIssue>(), warnings) : ValidationResult.Success;
    }

    public override IReadOnlyList<string> GetRequiredCapabilities()
    {
        var capabilities = new List<string> { "design-table" };
        var config = GetConfig();

        if (config.DataSource?.Type.Equals("sql", StringComparison.OrdinalIgnoreCase) == true)
        {
            capabilities.Add("sql-integration");
        }

        if (config.AutoUpdate)
        {
            capabilities.Add("auto-update");
        }

        return capabilities;
    }

    public override string ToVBACode()
    {
        var config = GetConfig();
        var lines = new List<string>
        {
            $"' Design Table: {Name}",
            $"Sub CreateDesignTable_{SanitizeName(Name)}()",
            "    Dim swApp As SldWorks.SldWorks",
            "    Dim swModel As SldWorks.ModelDoc2",
            "    Dim swDesignTable As SldWorks.DesignTable",
            "    ",
            "    Set swApp = Application.SldWorks",
            "    Set swModel = swApp.ActiveDoc",
            "    ",
            "    ' Create design table",
            $"    Set swDesignTable = swModel.InsertDesignTable(\"{config.TableName}\", True, False)",
            "    ",
        };

        foreach (var parameter in config.Parameters)
        {
            lines.Add($"    ' Add parameter: {parameter.Name}");
            lines.Add($"    swDesignTable.AddParameter \"{parameter.Name}\", \"{parameter.Type}\"");
        }

        if (config.Configurations is not null)
        {
            lines.Add("    ");
            lines.Add("    ' Add configurations");
            foreach (var configuration in config.Configurations)
            {
                lines.Add($"    swDesignTable.AddConfiguration \"{configuration.Name}\"");
                foreach (var pair in configuration.Values)
                {
                    lines.Add($"    swDesignTable.SetCellValue \"{configuration.Name}\", \"{pair.Key}\", \"{pair.Value}\"");
                }
            }
        }

        lines.Add("    ");
        lines.Add("    swDesignTable.UpdateTable");
        lines.Add("End Sub");
        return string.Join(Environment.NewLine, lines);
    }

    public override string ToMacroCode()
    {
        var config = GetConfig();
        return JsonSerializer.Serialize(new
        {
            type = Type,
            name = Name,
            actions = new[]
            {
                new
                {
                    action = "create-design-table",
                    parameters = new
                    {
                        tableName = config.TableName,
                        parameters = config.Parameters,
                        configurations = config.Configurations,
                    },
                },
            },
        }, JsonHelpers.SerializerOptions);
    }

    private DesignTableConfig GetConfig()
    {
        return new DesignTableConfig
        {
            TableName = GetRequiredString("tableName"),
            FileName = GetOptionalString("fileName"),
            Parameters = GetParameters(),
            Configurations = GetConfigurations(),
            DataSource = GetDataSource(),
            AutoUpdate = GetOptionalBoolean("autoUpdate"),
            Validation = null,
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

    private string? GetOptionalString(string key)
        => Properties.TryGetValue(key, out var value) ? value as string : null;

    private bool GetOptionalBoolean(string key)
        => Properties.TryGetValue(key, out var value) && value is bool b && b;

    private List<DesignTableParameter> GetParameters()
    {
        var items = JsonHelpers.AsList(Properties.TryGetValue("parameters", out var value) ? value : null);
        if (items is null)
        {
            return new List<DesignTableParameter>();
        }

        var parameters = new List<DesignTableParameter>();
        foreach (var item in items)
        {
            var dictionary = JsonHelpers.AsDictionary(item);
            if (dictionary is null)
            {
                continue;
            }

            parameters.Add(new DesignTableParameter
            {
                Name = Convert.ToString(dictionary.GetValueOrDefault("name")) ?? string.Empty,
                Type = Convert.ToString(dictionary.GetValueOrDefault("type")) ?? string.Empty,
                DataType = Convert.ToString(dictionary.GetValueOrDefault("dataType")) ?? string.Empty,
                DefaultValue = dictionary.GetValueOrDefault("defaultValue"),
                SqlColumn = Convert.ToString(dictionary.GetValueOrDefault("sqlColumn")),
                Formula = Convert.ToString(dictionary.GetValueOrDefault("formula")),
            });
        }

        return parameters;
    }

    private List<DesignTableConfigurationRow>? GetConfigurations()
    {
        var items = JsonHelpers.AsList(Properties.TryGetValue("configurations", out var value) ? value : null);
        if (items is null)
        {
            return null;
        }

        var configurations = new List<DesignTableConfigurationRow>();
        foreach (var item in items)
        {
            var dictionary = JsonHelpers.AsDictionary(item);
            if (dictionary is null)
            {
                continue;
            }

            var values = JsonHelpers.AsDictionary(dictionary.GetValueOrDefault("values")) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            configurations.Add(new DesignTableConfigurationRow
            {
                Name = Convert.ToString(dictionary.GetValueOrDefault("name")) ?? string.Empty,
                Values = values,
                Active = Convert.ToBoolean(dictionary.GetValueOrDefault("active") ?? false),
            });
        }

        return configurations;
    }

    private DesignTableDataSource? GetDataSource()
    {
        var dictionary = JsonHelpers.AsDictionary(Properties.TryGetValue("dataSource", out var value) ? value : null);
        if (dictionary is null)
        {
            return null;
        }

        return new DesignTableDataSource
        {
            Type = Convert.ToString(dictionary.GetValueOrDefault("type")) ?? string.Empty,
            ConnectionString = Convert.ToString(dictionary.GetValueOrDefault("connectionString")),
            Query = Convert.ToString(dictionary.GetValueOrDefault("query")),
            FilePath = Convert.ToString(dictionary.GetValueOrDefault("filePath")),
            ApiEndpoint = Convert.ToString(dictionary.GetValueOrDefault("apiEndpoint")),
            RefreshInterval = int.TryParse(Convert.ToString(dictionary.GetValueOrDefault("refreshInterval")), out var seconds) ? seconds : null,
        };
    }

    private async Task LoadFromSqlAsync(DesignTableConfig config, CancellationToken cancellationToken)
    {
        if (config.DataSource is null || string.IsNullOrWhiteSpace(config.DataSource.ConnectionString) || string.IsNullOrWhiteSpace(config.DataSource.Query))
        {
            throw new InvalidOperationException("SQL connection string and query are required");
        }

        try
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            var rows = GetSimulatedData();
            var configurations = rows.Select((row, index) => new DesignTableConfigurationRow
            {
                Name = row.TryGetValue("config_name", out var configName) ? Convert.ToString(configName) ?? $"Config_{index + 1}" : $"Config_{index + 1}",
                Values = MapSqlToParameters(config, row),
                Active = index == 0,
            }).ToList();

            Properties["configurations"] = configurations.Select(configuration => new Dictionary<string, object?>
            {
                ["name"] = configuration.Name,
                ["values"] = configuration.Values,
                ["active"] = configuration.Active,
            }).ToList();

            AppLogger.Info($"Loaded {configurations.Count} configurations from SQL");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to load from SQL", ex.Message);
            var rows = GetSimulatedData();
            Properties["configurations"] = rows.Select((row, index) => new Dictionary<string, object?>
            {
                ["name"] = Convert.ToString(row.GetValueOrDefault("config_name")) ?? $"Config_{index + 1}",
                ["values"] = MapSqlToParameters(config, row),
                ["active"] = index == 0,
            }).ToList();
        }
    }

    private static List<Dictionary<string, object?>> GetSimulatedData()
    {
        return
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["config_name"] = "Standard",
                ["length"] = 100,
                ["width"] = 50,
                ["height"] = 25,
                ["material"] = "Steel",
                ["finish"] = "Painted",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["config_name"] = "Large",
                ["length"] = 150,
                ["width"] = 75,
                ["height"] = 40,
                ["material"] = "Aluminum",
                ["finish"] = "Anodized",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["config_name"] = "Custom",
                ["length"] = 120,
                ["width"] = 60,
                ["height"] = 30,
                ["material"] = "Steel",
                ["finish"] = "Powder Coated",
            },
        ];
    }

    private Dictionary<string, object?> MapSqlToParameters(DesignTableConfig config, Dictionary<string, object?> row)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in config.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.SqlColumn) && row.TryGetValue(parameter.SqlColumn, out var value))
            {
                values[parameter.Name] = ConvertValue(value, parameter.DataType);
            }
            else if (!string.IsNullOrWhiteSpace(parameter.Formula))
            {
                values[parameter.Name] = EvaluateFormula(parameter.Formula, row);
            }
            else if (parameter.DefaultValue is not null)
            {
                values[parameter.Name] = parameter.DefaultValue;
            }
        }

        return values;
    }

    private static object? ConvertValue(object? value, string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "number" => Convert.ToDouble(value),
            "boolean" => Convert.ToBoolean(value),
            _ => Convert.ToString(value),
        };
    }

    private static string EvaluateFormula(string formula, Dictionary<string, object?> row)
    {
        var result = formula;
        foreach (var pair in row)
        {
            result = result.Replace($"{{{pair.Key}}}", Convert.ToString(pair.Value) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private object UpdateDesignTable(SolidWorksApi api, DesignTableConfig config)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["tableId"] = Id,
            ["configurations"] = config.Configurations?.Select(configuration => configuration.Name).ToList() ?? new List<string>(),
            ["parameters"] = config.Parameters.Select(parameter => parameter.Name).ToList(),
        };
    }

    private static string SanitizeName(string name)
    {
        var buffer = new char[name.Length];
        for (var i = 0; i < name.Length; i++)
        {
            buffer[i] = char.IsLetterOrDigit(name[i]) || name[i] == '_' ? name[i] : '_';
        }

        return new string(buffer);
    }
}
