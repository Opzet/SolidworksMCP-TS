# SolidWorks MCP Server

[![TypeScript](https://img.shields.io/badge/TypeScript-5.5-blue?logo=typescript)](https://www.typescriptlang.org/)
[![Node.js](https://img.shields.io/badge/Node.js-20+-green?logo=node.js)](https://nodejs.org/)
[![MCP](https://img.shields.io/badge/MCP-Compatible-green?logo=anthropic)](https://modelcontextprotocol.io)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows)](https://www.microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

SolidWorks MCP Server is a TypeScript MCP server for driving SolidWorks from AI clients over stdio. This branch implements the full server surface in one package: direct COM automation, VBA/macro workflows, resource/state management, SQL-backed design tables, optional PDM configuration flows, diagnostics, export tooling, and drawing/template utilities.

## What this branch includes

- **MCP server entry point** in `src/index.ts`
- **SolidWorks tool registry** spanning modeling, sketching, drawing, export, analysis, VBA, template management, diagnostics, and macro workflows
- **Stateful resources** for design tables and optional PDM configuration
- **Macro systems** for both recorded workflows and generated VBA
- **Database integration** for SQL-backed design table refresh
- **Persistence and infrastructure** including state storage, caching, logging, and DB connection management
- **Cross-platform development mode** via the mock SolidWorks adapter

## Tool surface

The server exposes dozens of MCP tools. Representative groups include:

| Area | Examples |
| --- | --- |
| Modeling | `open_model`, `create_part`, `create_extrusion`, `get_dimension`, `set_dimension`, `rebuild_model` |
| Sketch | `create_sketch`, `edit_sketch`, `sketch_line`, `sketch_circle`, `sketch_rectangle`, `add_sketch_constraint`, `add_sketch_dimension` |
| Drawing | `create_drawing_from_model`, `add_drawing_view`, `add_section_view`, `add_dimensions`, `update_sheet_format` |
| Drawing analysis | `get_drawing_sheet_info`, `get_drawing_views`, `set_drawing_scale`, `get_drawing_dimensions` |
| Export | `export_file`, `batch_export`, `export_with_options`, `capture_screenshot` |
| Analysis | `get_mass_properties`, `check_interference`, `measure_distance`, `check_geometry`, `get_bounding_box` |
| VBA generation | `generate_vba_script`, `create_feature_vba`, `create_batch_vba`, `create_drawing_vba` |
| Advanced VBA workflows | part, assembly, drawing, configuration, equation, file-management, simulation, and error-handling helpers |
| Macro workflows | `macro_start_recording`, `macro_stop_recording`, `macro_export_vba`, `start_native_macro_recording`, `stop_native_macro_recording`, `run_macro`, `edit_macro`, `batch_run_macros` |
| Templates | `extract_drawing_template`, `apply_drawing_template`, `batch_apply_template`, `compare_drawing_templates`, `save_template_to_library`, `list_template_library` |

## Resource and platform features

Beyond one-shot tools, the server also includes:

- **Design table resources** with optional SQL loading and refresh support
- **PDM configuration resources** gated behind `ENABLE_PDM=true`
- **State persistence** for resource instances
- **Database connection management** for SQL Server and PostgreSQL connection strings
- **Winston-based logging**
- **Mock adapter support** for CI and non-Windows development

## Requirements

### For real SolidWorks automation

- Windows 10/11
- SolidWorks 2021-2025 installed
- Node.js 20+
- A working local build of the optional `winax` dependency

### For development, CI, or non-Windows environments

- Node.js 20+
- `npm install --ignore-scripts`
- `USE_MOCK_SOLIDWORKS=true`

`winax` is Windows-only. On Linux and macOS, use the mock adapter for development and tests.

## Installation

### Windows with SolidWorks

```bash
git clone https://github.com/Opzet/SolidworksMCP-TS.git
cd SolidworksMCP-TS
npm install
npm run build
```

### Non-Windows or mock-only development

```bash
git clone https://github.com/Opzet/SolidworksMCP-TS.git
cd SolidworksMCP-TS
npm install --ignore-scripts
npm run build
```

## Running the server

```bash
npm run build
node dist/index.js
```

For local development:

```bash
npm run dev
```

## Claude Desktop configuration

Add the built server to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "solidworks": {
      "command": "node",
      "args": ["C:/path/to/SolidworksMCP-TS/dist/index.js"],
      "env": {
        "SOLIDWORKS_PATH": "C:\\Program Files\\SOLIDWORKS Corp\\SOLIDWORKS",
        "USE_MOCK_SOLIDWORKS": "false",
        "ENABLE_MACRO_RECORDING": "true",
        "LOG_LEVEL": "info"
      }
    }
  }
}
```

For mock-only usage:

```json
{
  "mcpServers": {
    "solidworks": {
      "command": "node",
      "args": ["C:/path/to/SolidworksMCP-TS/dist/index.js"],
      "env": {
        "USE_MOCK_SOLIDWORKS": "true"
      }
    }
  }
}
```

## Configuration

Common environment variables:

| Variable | Purpose |
| --- | --- |
| `SOLIDWORKS_PATH` | Optional SolidWorks install path |
| `SOLIDWORKS_VERSION` | Target version, default `2024` |
| `USE_MOCK_SOLIDWORKS` | Use the mock adapter instead of real COM |
| `ENABLE_MACRO_RECORDING` | Enable macro action capture |
| `ENABLE_PDM` | Enable PDM resource registration |
| `PDM_VAULT` | Vault name when PDM is enabled |
| `SQL_CONNECTION` | SQL-backed design table connection string |
| `PG_CONNECTION` | PostgreSQL connection string |
| `STATE_FILE` | Resource state persistence file |
| `STATE_AUTO_SAVE_INTERVAL` | Auto-save interval in ms |
| `ENABLE_CONNECTION_POOL` | Enable connection pool support |
| `CONNECTION_POOL_MAX_SIZE` | Pool size |
| `ENABLE_CIRCUIT_BREAKER` | Enable circuit breaker support |
| `CIRCUIT_BREAKER_THRESHOLD` | Failure threshold before opening breaker |
| `TEMPLATE_PART` / `TEMPLATE_ASSEMBLY` / `TEMPLATE_DRAWING` | Template paths |
| `LOG_LEVEL` / `LOG_FILE` | Logging configuration |
| `DEV_MODE` / `DEV_PORT` | Development settings |

## Architecture

```text
MCP client
   ↓
src/index.ts
   ↓
Tool registry + resource registry
   ↓
SolidWorks API + macro systems + DB/state/cache services
   ↓
winax COM automation or mock/testing path
```

Key code areas:

- `src/index.ts` - server bootstrap, tool/resource registration, request handlers
- `src/tools/` - MCP tool definitions
- `src/solidworks/api.ts` - low-level COM-facing SolidWorks API
- `src/resources/` - design table and PDM resource models
- `src/macro/` - macro recording/export infrastructure
- `src/db/` - DB connection management
- `src/state/` and `src/cache/` - persistence and caching
- `src/adapters/` - adapter experiments, routing, mock support, and infrastructure components

## Development

```bash
npm run build        # TypeScript compile
npm run dev          # Hot-reload dev server
npm test             # Vitest suite
npm run test:watch   # Watch mode
npm run test:coverage
npm run test:integration
npm run lint         # Biome check
npm run lint:fix
npm run format
npm run typecheck
npm run check        # TypeScript + Biome
```

## Testing reality

- The repository includes unit tests for core config/environment behavior.
- The normal test path uses the **mock SolidWorks adapter**.
- Real SolidWorks validation still requires a Windows machine with SolidWorks installed.
- `npm run test:integration` is intended for that real-environment path.

See [TESTING.md](TESTING.md) for the current testing guidance.

## Troubleshooting

- For `winax` install and Windows toolchain issues, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md).
- For branch-specific architecture notes, see `docs/`.

## Current limitations

- Real COM automation is Windows-only.
- Mock-based tests do not prove real SolidWorks behavior.
- Some advanced subsystems, especially PDM- and environment-specific workflows, still depend on the target machine and SolidWorks setup to validate end-to-end.

## License

MIT. See [LICENSE](LICENSE).
