namespace SolidworksMCP;

public sealed record AppConfiguration
{
    public string? SolidWorksPath { get; init; }

    public bool EnableMacroRecording { get; init; } = true;

    public bool EnablePdm { get; init; }

    public string? PdmVault { get; init; }

    public string? SqlConnection { get; init; }

    public string? StateFile { get; init; }

    public string? OutputRoot { get; init; }

    public string LogLevel { get; init; } = "info";

    public static AppConfiguration LoadFromEnvironment()
    {
        return new AppConfiguration
        {
            SolidWorksPath = Environment.GetEnvironmentVariable("SOLIDWORKS_PATH"),
            EnableMacroRecording = !string.Equals(Environment.GetEnvironmentVariable("ENABLE_MACRO_RECORDING"), "false", StringComparison.OrdinalIgnoreCase),
            EnablePdm = string.Equals(Environment.GetEnvironmentVariable("ENABLE_PDM"), "true", StringComparison.OrdinalIgnoreCase),
            PdmVault = Environment.GetEnvironmentVariable("PDM_VAULT"),
            SqlConnection = Environment.GetEnvironmentVariable("SQL_CONNECTION"),
            StateFile = Environment.GetEnvironmentVariable("STATE_FILE"),
            OutputRoot = Environment.GetEnvironmentVariable("SW_MCP_OUTPUT_ROOT"),
            LogLevel = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "info",
        };
    }
}
