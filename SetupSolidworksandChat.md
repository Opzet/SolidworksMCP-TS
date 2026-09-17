# Setup SolidWorks and Chat (Ollama + MCP)

## Goal
Use a local LLM (Ollama) to drive SolidWorks automation through this MCP server.

---

## Reference Architecture

```text
+-----------------------+        +-------------------------+
| Chat UI / App         |        | Ollama                  |
| (Open WebUI, custom)  |<------>| Local model runtime     |
+-----------+-----------+        +-------------------------+
			|
			| Tool-calling / MCP routing
			v
+-----------------------+
| MCP Host / Orchestrator|
| (must support MCP)    |
+-----------+-----------+
			|
			| stdio JSON-RPC (initialize/tools/list/tools/call)
			v
+-----------------------+
| SolidWorks MCP Server |
| (this repo, .NET/TS)  |
+-----------+-----------+
			|
			| COM / VBA
			v
+-----------------------+
| SolidWorks Desktop    |
| (Windows)             |
+-----------------------+
```

---

## Key Point
**Ollama alone is not enough.**
You need an **MCP-capable host/orchestrator** between the model and the SolidWorks MCP server.

- Ollama = model inference
- MCP host = tool discovery/calling + JSON-RPC wiring
- SolidWorks MCP server = CAD tool execution

---

## Deployment Pattern (Single Windows Workstation)

1. **Windows machine with SolidWorks installed**
2. **Ollama** running locally
3. **Chat app** connected to Ollama
4. **MCP host** configured to launch `SolidworksMCP` over stdio
5. Chat prompt -> model decides tool calls -> MCP host calls SolidWorks tools

---

## Minimal MCP Wiring Contract
The MCP host should support at least:

- `initialize`
- `tools/list`
- `tools/call`
- `resources/list`
- `resources/read`

Your server already exposes these JSON-RPC flows over stdio.

---

## Practical Setup Sequence

## 1) Start SolidWorks
Open SolidWorks normally (licensed desktop app).

## 2) Run SolidWorks MCP server
Use either:
- `.NET build output` (for C# server), or
- `node dist/index.js` (for TS server)

## 3) Start Ollama
Example:
```powershell
ollama serve
```
Pull a tool-friendly model (example):
```powershell
ollama pull llama3.1:8b
```

## 4) Configure chat app + orchestrator
- Chat app model endpoint -> Ollama
- Tool backend -> MCP host
- MCP host -> launches SolidWorks MCP server over stdio

## 5) Validate end-to-end
Send prompts like:
- "List available SolidWorks tools"
- "Create a part and add a sketch on Front plane"
- "Extrude 20mm"

---

## Security / Safety Recommendations

- Keep all processes local on trusted workstation.
- Restrict file export directories.
- Require explicit confirmation for destructive operations.
- Log all tool invocations (`tools/call`) with timestamp and parameters.

---

## Troubleshooting Map

- **Model answers but no CAD action**
  - MCP host not connected to SolidWorks MCP server.
- **Server runs but tool calls fail**
  - SolidWorks not running / COM access issue.
- **Chat works but no tool-use behavior**
  - Model or app not configured for tool-calling.
- **Intermittent failures on complex features**
  - Ensure VBA fallback paths are enabled and macro execution permissions are allowed.

---

## Recommended Baseline Stack

- **Windows 10/11**
- **SolidWorks 2021+**
- **Ollama** (local)
- **MCP-capable orchestrator** (required)
- **This SolidWorks MCP server**

This gives a fully local, privacy-preserving CAD assistant architecture.

---

## Concrete Remote Ollama Config (Your `aibox` Node)

Use this when Ollama is hosted on another machine and SolidWorks MCP runs on the local SolidWorks workstation.

### Environment Verified
- Ollama endpoint: `http://aibox:11434`
- Version: `0.34.0`
- Example model available: `qwen2.5-coder:14b`

### 1) Verify Ollama connectivity from the SolidWorks workstation
```powershell
curl.exe http://aibox:11434/api/version
curl.exe http://aibox:11434/api/tags
```

### 2) Quick model generation sanity check
```powershell
Invoke-RestMethod -Method Post -Uri "http://aibox:11434/api/generate" -ContentType "application/json" -Body (@{
  model  = "qwen2.5-coder:14b"
  prompt = "Reply with: ollama ok"
  stream = $false
} | ConvertTo-Json)
```

### 3) Start SolidWorks MCP server locally
From this repo (`C#\SolidworksMCP`):
```powershell
dotnet run --project .\SolidworksMCP.csproj
```

### 4) MCP host/orchestrator wiring (reference JSON)
> Adjust keys to your host's schema. This is a reference pattern.

```json
{
  "models": {
    "default": {
      "provider": "ollama",
      "baseUrl": "http://aibox:11434",
      "model": "qwen2.5-coder:14b",
      "temperature": 0.1
    }
  },
  "mcpServers": {
    "solidworks": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:/source/repos/SolidworksMCP-TS/C#/SolidworksMCP/SolidworksMCP.csproj"
      ],
      "env": {
        "SOLIDWORKS_PART_TEMPLATE": "C:/ProgramData/SOLIDWORKS/SOLIDWORKS 2024/templates/Part.prtdot"
      }
    }
  }
}
```

### 5) End-to-end validation prompts
Use your chat client and ask:
1. `List available SolidWorks tools`
2. `Create a part`
3. `Create a sketch on Front plane`
4. `Add a line from (0,0,0) to (100,0,0)`
5. `Extrude 20 mm`

---

## Network and Reliability Notes for Remote Ollama
- Keep Ollama and workstation on low-latency LAN.
- Prefer hostname (`aibox`) over unstable dynamic IP.
- Set request timeout >= 120s for larger prompts/models.
- If tool-calling becomes inconsistent under load, reduce temperature and context length.
- Keep SolidWorks MCP server local to the workstation running SolidWorks (COM is local).
