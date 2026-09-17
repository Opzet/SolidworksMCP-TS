# SolidWorks Chat Client (Ollama + MCP)

WinForms orchestrator client that:
- uses **Ollama** for LLM responses/tool planning
- **launches the SolidworksMCP server** over MCP JSON-RPC stdio
- executes MCP tool calls against the launched server process

## Repository URL
- https://github.com/Opzet/SolidworksMCP-TS

## Run
From `C#\SolidworksChatClient`:

```powershell
dotnet run
```

## Start the demo
1. Open **Settings** tab.
2. Verify Ollama/model and MCP launch values.
3. Click **Connect** (this launches MCP).
4. Switch to **Workspace** tab.
5. Click **Demo: 4-Bar Linkage GA + Parts**.

The app starts a WinForms UI and acts as the **orchestrator**.

- Open the **Settings** tab for technical configuration and click **Connect** to launch MCP.
- Return to **Workspace** for chat, image decode, and demo actions.

By default it uses:
- `OLLAMA_BASE_URL=http://aibox:11434`
- `OLLAMA_MODEL=qwen2.5-coder:14b`
- MCP launch command: `D:/source/repos/SolidworksMCP-TS/C#/SolidworksMCP/bin/Debug/net9.0/SolidworksMCP.exe`
- MCP launch args: *(empty)*

Override with env vars:

```powershell
$env:OLLAMA_BASE_URL = "http://aibox:11434"
$env:OLLAMA_MODEL = "qwen2.5-coder:14b"
$env:MCP_SERVER_COMMAND = "D:/source/repos/SolidworksMCP-TS/C#/SolidworksMCP/bin/Debug/net9.0/SolidworksMCP.exe"
$env:MCP_SERVER_ARGS = ""
dotnet run
```

## Notes
- SolidWorks must be installed and running on the same Windows machine as the MCP server.
- The orchestrator (`SolidworksChatClient`) is responsible for launching MCP.
- Use a **compiled MCP executable**; do not use `dotnet run` in orchestrator launch settings.
- Technical launch settings are intentionally separated into the **Settings** tab.
- The layout is responsive: at smaller window widths, the right-side media/actions panel collapses to prioritize chat.
- Close the WinForms window to quit.
