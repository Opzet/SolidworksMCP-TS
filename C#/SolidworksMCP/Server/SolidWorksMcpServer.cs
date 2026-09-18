namespace SolidworksMCP;

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class SolidWorksMcpServer
{
    private readonly AppConfiguration configuration;
    private readonly SolidWorksApi api;
    private readonly ResourceStateStore stateStore;
    private readonly MacroRecorder macroRecorder;
    private readonly CacheManager cacheManager;
    private readonly IReadOnlyList<McpToolDefinition> tools;
    private readonly CancellationTokenSource shutdownCts = new();
    private readonly string serverName = "solidworks-mcp-server";
    private readonly string serverVersion;

    public SolidWorksMcpServer()
    {
        configuration = AppConfiguration.LoadFromEnvironment();
        AppLogger.Configure(configuration.LogLevel);
        api = new SolidWorksApi(configuration.OutputRoot);
        stateStore = new ResourceStateStore(configuration.StateFile);
        macroRecorder = new MacroRecorder();
        cacheManager = new CacheManager(1000, TimeSpan.FromHours(1));
        serverVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        ResourceFactory.RegisterDefaults(configuration);
        tools = BuildTools();
        SetupMacroHandlers();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token, cancellationToken);
        await stateStore.LoadAsync(linked.Token).ConfigureAwait(false);
        AppLogger.Info("State store loaded", stateStore.GetStatistics());

        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            await ProcessMessagesAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            await ShutdownAsync().ConfigureAwait(false);
        }
    }

    public async Task ShutdownAsync()
    {
        AppLogger.Info("Starting server shutdown...");
        shutdownCts.Cancel();

        try
        {
            await stateStore.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to save state during shutdown", ex.Message);
        }

        stateStore.StopAutoSave();
        cacheManager.Clear();
        macroRecorder.Clear();

        if (api.IsConnected())
        {
            api.Disconnect();
        }

        await stateStore.DisposeAsync().ConfigureAwait(false);
        AppLogger.Info("Server shutdown complete");
    }

    private IReadOnlyList<McpToolDefinition> BuildTools()
    {
        var result = new List<McpToolDefinition>(ToolCatalog.GetAllTools(configuration));
        result.AddRange(BuildMacroTools());
        result.AddRange(BuildResourceTools());
        return result;
    }

    private IReadOnlyList<McpToolDefinition> BuildMacroTools()
    {
        return
        [
            new McpToolDefinition("macro_start_recording", "Start recording a new macro", JsonSchemaBuilder.ObjectWithRequired(["name"], ("name", JsonSchemaBuilder.String()), ("description", JsonSchemaBuilder.String())), HandleMacroStartRecording),
            new McpToolDefinition("macro_stop_recording", "Stop the current macro recording", JsonSchemaBuilder.Object(), HandleMacroStopRecording),
            new McpToolDefinition("macro_export_vba", "Export a recorded macro to VBA code", JsonSchemaBuilder.ObjectWithRequired(["macroId"], ("macroId", JsonSchemaBuilder.String())), HandleMacroExportVba),
        ];
    }

    private IReadOnlyList<McpToolDefinition> BuildResourceTools()
    {
        var tools = new List<McpToolDefinition>
        {
            new("design_table_create", "Create a new design table with optional SQL data source", JsonSchemaBuilder.ObjectWithRequired(["name", "config"], ("name", JsonSchemaBuilder.String()), ("config", JsonSchemaBuilder.Any())), HandleDesignTableCreate),
            new("design_table_refresh", "Refresh design table data from SQL source", JsonSchemaBuilder.ObjectWithRequired(["resourceId"], ("resourceId", JsonSchemaBuilder.String())), HandleDesignTableRefresh),
        };

        if (configuration.EnablePdm)
        {
            tools.Add(new McpToolDefinition("pdm_configure", "Configure PDM vault settings and operations", JsonSchemaBuilder.ObjectWithRequired(["name", "config"], ("name", JsonSchemaBuilder.String()), ("config", JsonSchemaBuilder.Any())), HandlePdmConfigure));
        }

        return tools;
    }

    private void SetupMacroHandlers()
    {
        macroRecorder.RegisterActionHandler("create-sketch", action => new ValueTask<object?>(api.CreateSketch(action.Parameters)));
        macroRecorder.RegisterActionHandler("add-line", action => new ValueTask<object?>(api.AddLine(action.Parameters)));
        macroRecorder.RegisterActionHandler("extrude", action => new ValueTask<object?>(api.CreateExtrude(ToolHelpers.GetDouble(action.Parameters, "depth"), ToolHelpers.GetDouble(action.Parameters, "draft"), ToolHelpers.GetBool(action.Parameters, "reverse"))));
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var input = Console.OpenStandardInput();
        using var reader = new StreamReader(input, Encoding.UTF8, leaveOpen: true);
        var output = Console.OpenStandardOutput();

        while (!cancellationToken.IsCancellationRequested)
        {
            var request = await ReadRequestAsync(reader, cancellationToken).ConfigureAwait(false);
            if (request is null)
            {
                break;
            }

            var response = await HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
            if (response is not null)
            {
                await WriteResponseAsync(output, response, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<JsonRpcRequest?> ReadRequestAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        do
        {
            line = await reader.ReadLineAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return null;
            }
        } while (string.IsNullOrEmpty(line));

        while (!string.IsNullOrEmpty(line))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex > 0)
            {
                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                headers[key] = value;
            }

            line = await reader.ReadLineAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return null;
            }
        }

        if (!headers.TryGetValue("Content-Length", out var contentLengthText) || !int.TryParse(contentLengthText, out var contentLength))
        {
            throw new InvalidOperationException("Missing Content-Length header");
        }

        var buffer = new char[contentLength];
        var read = 0;
        while (read < contentLength)
        {
            var current = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read), cancellationToken).ConfigureAwait(false);
            if (current == 0)
            {
                return null;
            }

            read += current;
        }

        var json = new string(buffer, 0, read);
        return JsonSerializer.Deserialize<JsonRpcRequest>(json, JsonHelpers.SerializerOptions);
    }

    private static async Task WriteResponseAsync(Stream output, JsonRpcResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, JsonHelpers.SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleListTools(request),
                "tools/call" => await HandleCallToolAsync(request, cancellationToken).ConfigureAwait(false),
                "resources/list" => HandleListResources(request),
                "resources/read" => HandleReadResource(request),
                "ping" => new { },
                _ => throw new InvalidOperationException($"Method '{request.Method}' not found"),
            };

            return request.Id is null ? null : new JsonRpcResponse { Id = request.Id, Result = result };
        }
        catch (Exception ex)
        {
            if (request.Id is null)
            {
                return null;
            }

            var code = ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ? JsonRpcErrorCodes.MethodNotFound : JsonRpcErrorCodes.InternalError;
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = code,
                    Message = ex.Message,
                },
            };
        }
    }

    private object HandleInitialize(JsonRpcRequest request)
    {
        _ = request;
        return new
        {
            protocolVersion = "2024-11-05",
            serverInfo = new
            {
                name = serverName,
                version = serverVersion,
                description = "Enhanced SolidWorks MCP Server with macro recording, design tables, SQL integration, and PDM support",
            },
            capabilities = new
            {
                tools = new { },
                resources = new { },
            },
        };
    }

    private object HandleListTools(JsonRpcRequest request)
    {
        _ = request;
        return new
        {
            tools = tools.Select(tool => new
            {
                name = tool.Name,
                description = tool.Description,
                inputSchema = tool.InputSchema,
            }).ToArray(),
        };
    }

    private async Task<object> HandleCallToolAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var parameters = request.Parameters.HasValue ? request.Parameters.Value.ToDictionary() : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var name = parameters.TryGetValue("name", out var nameValue) ? Convert.ToString(nameValue) ?? string.Empty : string.Empty;
        var tool = tools.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Tool \"{name}\" not found");

        Dictionary<string, object?>? arguments = null;
        if (parameters.TryGetValue("arguments", out var argumentValue))
        {
            arguments = JsonHelpers.AsDictionary(argumentValue) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        AppLogger.Operation(name, "started", arguments);

        try
        {
            if (configuration.EnableMacroRecording)
            {
                try
                {
                    macroRecorder.RecordAction(name, tool.Description, arguments ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
                }
                catch
                {
                }
            }

            if (!api.IsConnected())
            {
                api.Connect();
            }

            JsonElement? jsonArguments = arguments is null ? (JsonElement?)null : JsonHelpers.ToJsonElement(arguments);
            var result = await tool.Handler(jsonArguments, api, cancellationToken).ConfigureAwait(false);
            AppLogger.Operation(name, "completed", new { result });

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = result is string text ? text : JsonSerializer.Serialize(result, JsonHelpers.SerializerOptions),
                    },
                },
            };
        }
        catch (Exception ex)
        {
            AppLogger.Operation(name, "failed", new { error = ex.Message });
            if (ex is JsonException)
            {
                throw new InvalidOperationException($"Invalid parameters: {ex.Message}");
            }

            throw;
        }
    }

    private object HandleListResources(JsonRpcRequest request)
    {
        _ = request;
        var resources = stateStore.GetAllStates();
        return new
        {
            resources = resources.Select(state => new
            {
                uri = $"solidworks://{state.Type}/{state.Id}",
                name = state.Name,
                mimeType = "application/json",
                description = $"{state.Type} resource: {state.Name}",
            }).ToArray(),
        };
    }

    private object HandleReadResource(JsonRpcRequest request)
    {
        var parameters = request.Parameters.HasValue ? request.Parameters.Value.ToDictionary() : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var uri = parameters.TryGetValue("uri", out var uriValue) ? Convert.ToString(uriValue) ?? string.Empty : string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(uri, "^solidworks://([^/]+)/(.+)$");
        if (!match.Success)
        {
            throw new InvalidOperationException("Invalid resource URI");
        }

        var id = match.Groups[2].Value;
        var state = stateStore.GetState(id) ?? throw new InvalidOperationException("Resource not found");
        var text = JsonSerializer.Serialize(state, JsonHelpers.SerializerOptions);
        return new
        {
            contents = new[]
            {
                new
                {
                    uri,
                    mimeType = "application/json",
                    text,
                },
            },
        };
    }

    private ValueTask<object?> HandleMacroStartRecording(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = api;
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        var id = macroRecorder.StartRecording(ToolHelpers.GetString(args, "name"), ToolHelpers.GetString(args, "description"));
        return ValueTask.FromResult<object?>(new { macroId = id, status = "recording" });
    }

    private ValueTask<object?> HandleMacroStopRecording(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = arguments;
        _ = api;
        _ = cancellationToken;
        var recording = macroRecorder.StopRecording();
        return ValueTask.FromResult<object?>(recording is null ? new { error = "No recording in progress" } : recording);
    }

    private ValueTask<object?> HandleMacroExportVba(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        _ = api;
        _ = cancellationToken;
        var args = ToolHelpers.ToArguments(arguments);
        return ValueTask.FromResult<object?>(new { code = macroRecorder.ExportToVba(ToolHelpers.GetString(args, "macroId")) });
    }

    private async ValueTask<object?> HandleDesignTableCreate(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        var args = ToolHelpers.ToArguments(arguments);
        var resource = new DesignTableResource($"dt_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", ToolHelpers.GetString(args, "name"), ToolHelpers.GetDictionary(args, "config"));
        var result = await resource.ExecuteAsync(api, cancellationToken).ConfigureAwait(false);
        await stateStore.SetStateAsync(resource.Id, resource.ToState(), cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async ValueTask<object?> HandleDesignTableRefresh(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        var args = ToolHelpers.ToArguments(arguments);
        var resourceId = ToolHelpers.GetString(args, "resourceId");
        var state = stateStore.GetState(resourceId) ?? throw new InvalidOperationException("Design table resource not found");
        if (!string.Equals(state.Type, "design-table", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Design table resource not found");
        }

        var resource = new DesignTableResource(state.Id, state.Name, state.Properties);
        await resource.RefreshAsync(api, cancellationToken).ConfigureAwait(false);
        await stateStore.SetStateAsync(resource.Id, resource.ToState(), cancellationToken).ConfigureAwait(false);
        return new { status = "refreshed" };
    }

    private async ValueTask<object?> HandlePdmConfigure(JsonElement? arguments, SolidWorksApi api, CancellationToken cancellationToken)
    {
        var args = ToolHelpers.ToArguments(arguments);
        var resource = new PDMResource($"pdm_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", ToolHelpers.GetString(args, "name"), ToolHelpers.GetDictionary(args, "config"));
        var result = await resource.ExecuteAsync(api, cancellationToken).ConfigureAwait(false);
        await stateStore.SetStateAsync(resource.Id, resource.ToState(), cancellationToken).ConfigureAwait(false);
        return result;
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        _ = sender;
        e.Cancel = true;
        shutdownCts.Cancel();
    }
}
