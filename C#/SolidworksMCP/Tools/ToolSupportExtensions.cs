namespace SolidworksMCP;

using System.Text.Json;

internal static class ToolSupportExtensions
{
    public static bool IsTrue(this object? value) => value is bool boolean && boolean;
}
