# AutoWorks - SolidWorks MCP Server and Chat Client in C#

[![MCP Compatible](https://img.shields.io/badge/MCP-Compatible-green?logo=anthropic)](https://modelcontextprotocol.io)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows)](https://www.microsoft.com/windows)

AutoWorks is a C# desktop port of the SolidWorks MCP workflow. It includes a Windows desktop client and a local MCP server that automates SolidWorks through COM interop.

> **Project Status: Alpha / Experimental**
>
> The C# port is active and still being validated against SolidWorks 2026. Expect rough edges, COM quirks, and incomplete coverage in some tool areas.

## How It Works

The solution is split into two main apps:

- **AutoWorks** - the Windows desktop chat client and orchestrator
- **SolidworksMCP** - the local MCP server process launched over stdio

The server exposes SolidWorks operations as MCP tools over stdio. The client connects to that process, displays status, and routes user prompts into tool calls.

### Routing model

- **Preferred path**: direct COM calls for supported and stable SolidWorks API operations
- **Explicit macro tools**: VBA generation / native macro features exist as separate tools for macro authoring and execution
- **Failure handling**: operations return structured error context so the client can show what failed and why

This repo does **not** use the old TypeScript / Node.js runtime path.

## Prerequisites

- **Windows 10/11**
- **SolidWorks 2026** installed and licensed
- **.NET 9 SDK**
- A compatible MCP client or the included **AutoWorks** desktop app

## Solution structure

- `C#\AutoWorks` - Windows desktop chat client built with WinForms + Blazor WebView + MudBlazor
- `C#\SolidworksMCP` - local MCP server that talks to SolidWorks via COM
- `C#\SolidworksMCP.Tests` - automated tests

## Build and run

1. Open `C#\SolidworksMCP\SolidworksMCP.slnx` in Visual Studio.
2. Set **AutoWorks** as the startup project for the desktop client experience.
3. Build the solution.
4. Launch AutoWorks and connect it to the local `SolidworksMCP.exe` process.

For publish scenarios, build the `SolidworksMCP` server first and then publish `AutoWorks` so the server runtime files are copied into the published output.

## Configuration

The server reads these environment variables:

- `SOLIDWORKS_PATH`
- `ENABLE_MACRO_RECORDING`
- `ENABLE_PDM`
- `PDM_VAULT`
- `SQL_CONNECTION`
- `STATE_FILE`
- `SW_MCP_OUTPUT_ROOT`
- `LOG_LEVEL`

## Available capabilities

The server registers tools across these categories:

| Category | Example tools | Status |
|----------|---------------|--------|
| **Modeling** | `create_part`, `create_assembly`, `save_document`, `rebuild_model`, `create_extrusion`, `create_revolve` | Actively developed |
| **Sketch** | `create_sketch`, `add_line`, `add_circle`, `add_rectangle`, `list_sketches`, `list_sketch_segments` | Actively developed |
| **Drawing** | `create_drawing_from_model`, `add_drawing_view`, `add_section_view`, `list_sheets`, `list_drawing_views` | Actively developed |
| **Analysis** | `get_mass_properties`, `check_interference`, `measure_distance`, `check_geometry` | Actively developed |
| **Export** | `export_file`, `batch_export`, `capture_screenshot` | Actively developed |
| **Macro / VBA** | `generate_vba_script`, `create_feature_vba`, `create_batch_vba`, `macro_start_recording`, `macro_stop_recording` | Available |
| **Template Manager** | `extract_drawing_template`, `apply_drawing_template`, `save_drawing_template` | Available |
| **Resources** | `design-table`, `pdm-configuration` | Available / optional |

## Missing skills / planned areas

The client also shows the current missing skill areas explicitly so gaps stay visible during development.

Missing skills currently tracked:

- `diagnostics`
- `drawing-analysis`
- `enhanced-drawing`
- `extrusion-helper`
- `macro-security`
- `vba-advanced`
- `vba-assembly`
- `vba-drawing`
- `vba-file-management`
- `vba-part`

## Architecture

```text
AutoWorks (desktop client)
    |
    | launches
    v
SolidworksMCP.exe  --stdio-->  MCP transport
    |
    | direct COM interop
    v
SolidWorks COM API
```

## Key design decisions

- **Direct COM first**: supported SolidWorks API calls are invoked directly when the interop path is stable.
- **No blanket VBA fallback**: VBA generation is available as an explicit capability, but it is not used to hide unclear COM failures.
- **Clear COM diagnostics**: failures should surface the real COM or SolidWorks error context.
- **Avoid `null` for optional COM arguments** where the bridge expects omission/`undefined` semantics.
- **Prefer feature-tree traversal for discovery**: use `FeatureByPositionReverse()` and `GetTypeName2()` where it is more reliable than `SelectByID2`.
- **StdIO logging discipline**: do not write `console.*` style output on the MCP transport path; keep transport messages clean.

## What has been validated

Based on local development testing:

- Connecting to a running SolidWorks instance via COM
- Creating sketch planes and basic sketch geometry
- Simple extrusions with limited parameters
- Feature-tree traversal for sketch discovery
- VBA generation helpers for explicit macro tools

## Known limitations

- SolidWorks tool coverage is still incomplete.
- Some tool areas are only partially validated on real SolidWorks 2026.
- Performance and resiliency testing is still limited.
- CI is not yet running full SolidWorks integration tests.

## Claude Desktop / external MCP clients

Use the published `SolidworksMCP.exe` as the command in your MCP client configuration.

```json
{
  "mcpServers": {
    "solidworks": {
      "command": "C:\\path\\to\\publish\\SolidworksMCP.exe",
      "args": [],
      "env": {
        "SOLIDWORKS_PATH": "C:\\Program Files\\SOLIDWORKS Corp\\SOLIDWORKS",
        "LOG_LEVEL": "info"
      }
    }
  }
}
```

## Roadmap

- [ ] Comprehensive integration test suite on real SolidWorks
- [ ] CI with a self-hosted Windows runner
- [ ] End-to-end validation of all modeling tools
- [ ] End-to-end validation of drawing and export tools
- [ ] Better diagnostics for fragile COM calls
- [ ] Performance benchmarking with real metrics

## License

MIT - See [LICENSE](LICENSE)

## Acknowledgments

- [Anthropic MCP](https://modelcontextprotocol.io)
- SolidWorks API documentation
- The C# / .NET desktop stack used by AutoWorks
