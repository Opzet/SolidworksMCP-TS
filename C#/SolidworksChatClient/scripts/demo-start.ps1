param(
	[string]$ProjectPath = "D:\source\repos\SolidworksMCP-TS\C#\SolidworksChatClient\SolidworksChatClient.csproj",
	[string]$McpExePath = "D:\source\repos\SolidworksMCP-TS\C#\SolidworksMCP\bin\Debug\net9.0\SolidworksMCP.exe"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $McpExePath)) {
	Write-Host "Compiled MCP server not found:" -ForegroundColor Red
	Write-Host "  $McpExePath" -ForegroundColor Red
	Write-Host "Build SolidworksMCP once in Visual Studio first, then rerun this script." -ForegroundColor Yellow
	exit 1
}

Write-Host "[1/2] Launching WinForms orchestrator (no MCP compile at launch)..." -ForegroundColor Cyan
Write-Host "[2/2] In app: Settings -> verify MCP path -> Connect -> Workspace -> Demo: 4-Bar Linkage GA + Parts" -ForegroundColor Green

$env:MCP_SERVER_COMMAND = $McpExePath
$env:MCP_SERVER_ARGS = ""

dotnet run --project $ProjectPath
