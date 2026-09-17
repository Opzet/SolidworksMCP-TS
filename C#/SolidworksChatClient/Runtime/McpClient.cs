namespace SolidworksChatClient.Runtime;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class McpClient : IDisposable
{
    private readonly Process process;
    private readonly string command;
    private Stream? stdin;
    private Stream? stdout;
    private readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private int nextId = 1;

    public McpClient(string command, string[] args)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("MCP command is required.", nameof(command));
        }

        this.command = command;

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        process = new Process { StartInfo = startInfo };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                Debug.WriteLine($"mcp-err> {eventArgs.Data}");
            }
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        EnsureSingleServerInstance();
        cancellationToken.ThrowIfCancellationRequested();

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start MCP server process.");
        }

        process.BeginErrorReadLine();
        stdin = process.StandardInput.BaseStream;
        stdout = process.StandardOutput.BaseStream;

        _ = await SendRequestAsync("initialize", new JsonObject(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListToolNamesAsync(CancellationToken cancellationToken = default)
    {
        var tools = await ListToolsAsync(cancellationToken).ConfigureAwait(false);
        var names = new List<string>(tools.Count);

        foreach (var node in tools)
        {
            if (node is not JsonObject tool)
            {
                continue;
            }

            var name = tool["name"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    public async Task<JsonArray> ListToolsAsOllamaToolsAsync()
    {
        var tools = await ListToolsAsync().ConfigureAwait(false);
        var ollamaTools = new JsonArray();
        foreach (var node in tools)
        {
            if (node is not JsonObject tool)
            {
                continue;
            }

            ollamaTools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool["name"]?.GetValue<string>() ?? string.Empty,
                    ["description"] = tool["description"]?.GetValue<string>() ?? string.Empty,
                    ["parameters"] = tool["inputSchema"]?.DeepClone() ?? new JsonObject(),
                }
            });
        }

        return ollamaTools;
    }

    public async Task<string> CallToolAsync(string toolName, JsonObject arguments)
    {
        var result = await SendRequestAsync("tools/call", new JsonObject
        {
            ["name"] = toolName,
            ["arguments"] = arguments,
        }).ConfigureAwait(false);

        var content = result["content"] as JsonArray;
        if (content is { Count: > 0 } && content[0] is JsonObject first)
        {
            return first["text"]?.GetValue<string>() ?? result.ToJsonString();
        }

        return result.ToJsonString();
    }

    private async Task<JsonArray> ListToolsAsync()
    {
        var result = await SendRequestAsync("tools/list", new JsonObject()).ConfigureAwait(false);
        return result["tools"] as JsonArray ?? [];
    }

    private async Task<JsonObject> SendRequestAsync(string method, JsonObject @params)
    {
        var requestId = nextId++;

        await WriteMessageAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = method,
            ["params"] = @params,
        }).ConfigureAwait(false);

        while (true)
        {
            var response = await ReadMessageAsync().ConfigureAwait(false);
            if (response["id"]?.GetValue<int>() != requestId)
            {
                continue;
            }

            if (response["error"] is JsonObject error)
            {
                throw new InvalidOperationException($"MCP error {error["code"]}: {error["message"]}");
            }

            return response["result"] as JsonObject ?? new JsonObject();
        }
    }

    private async Task WriteMessageAsync(JsonObject message)
    {
        var input = stdin ?? throw new InvalidOperationException("MCP stdin not initialized.");

        var payload = Encoding.UTF8.GetBytes(message.ToJsonString(serializerOptions));
        var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");

        await input.WriteAsync(header).ConfigureAwait(false);
        await input.WriteAsync(payload).ConfigureAwait(false);
        await input.FlushAsync().ConfigureAwait(false);
    }

    private async Task<JsonObject> ReadMessageAsync()
    {
        var output = stdout ?? throw new InvalidOperationException("MCP stdout not initialized.");
        var contentLength = await ReadContentLengthAsync(output).ConfigureAwait(false);

        var buffer = new byte[contentLength];
        var offset = 0;
        while (offset < contentLength)
        {
            var read = await output.ReadAsync(buffer.AsMemory(offset, contentLength - offset)).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("MCP server closed stdout while reading payload.");
            }

            offset += read;
        }

        var json = Encoding.UTF8.GetString(buffer);
        return JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Invalid MCP JSON-RPC payload.");
    }

    private static async Task<int> ReadContentLengthAsync(Stream output)
    {
        var bytes = new List<byte>();
        var one = new byte[1];

        while (true)
        {
            var read = await output.ReadAsync(one).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("MCP server closed stdout while reading headers.");
            }

            bytes.Add(one[0]);
            var count = bytes.Count;
            if (count >= 4
                && bytes[count - 4] == '\r'
                && bytes[count - 3] == '\n'
                && bytes[count - 2] == '\r'
                && bytes[count - 1] == '\n')
            {
                break;
            }
        }

        var headerText = Encoding.ASCII.GetString([.. bytes]);
        foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            if (!key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (int.TryParse(value, out var length) && length > 0)
            {
                return length;
            }
        }

        throw new InvalidOperationException("Missing or invalid Content-Length header from MCP server.");
    }

    private void EnsureSingleServerInstance()
    {
        if (!Path.IsPathRooted(command) || !File.Exists(command))
        {
            return;
        }

        var commandPath = Path.GetFullPath(command);
        var processName = Path.GetFileNameWithoutExtension(commandPath);
        foreach (var existingProcess in Process.GetProcessesByName(processName))
        {
            if (existingProcess.Id == Environment.ProcessId)
            {
                continue;
            }

            if (!IsSameExecutable(existingProcess, commandPath))
            {
                continue;
            }

            try
            {
                existingProcess.Kill(entireProcessTree: true);
                _ = existingProcess.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to terminate existing MCP process '{processName}' ({existingProcess.Id}): {ex.Message}", ex);
            }
        }
    }

    private static bool IsSameExecutable(Process process, string commandPath)
    {
        try
        {
            var mainModulePath = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(mainModulePath)
                && string.Equals(Path.GetFullPath(mainModulePath), commandPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignored
        }

        stdin?.Dispose();
        stdout?.Dispose();
        process.Dispose();
    }
}
