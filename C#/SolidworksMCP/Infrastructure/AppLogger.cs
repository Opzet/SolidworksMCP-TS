namespace SolidworksMCP;

using System.Globalization;
using System.Text.Json;

public enum AppLogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

internal static class AppLogger
{
    private static readonly object Gate = new();
    private static AppLogLevel minimumLevel = AppLogLevel.Info;

    public static void Configure(string? level)
    {
        minimumLevel = ParseLevel(level);
    }

    public static void Debug(string message, object? data = null) => Write(AppLogLevel.Debug, message, data);

    public static void Info(string message, object? data = null) => Write(AppLogLevel.Info, message, data);

    public static void Warn(string message, object? data = null) => Write(AppLogLevel.Warn, message, data);

    public static void Error(string message, object? data = null) => Write(AppLogLevel.Error, message, data);

    public static void Operation(string operation, string state, object? data = null) => Write(AppLogLevel.Info, $"{operation} {state}", data);

    private static void Write(AppLogLevel level, string message, object? data)
    {
        if (level < minimumLevel)
        {
            return;
        }

        lock (Gate)
        {
            var payload = new Dictionary<string, object?>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["level"] = level.ToString().ToLowerInvariant(),
                ["message"] = message,
            };

            if (data is not null)
            {
                payload["data"] = data;
            }

            Console.Error.WriteLine(JsonSerializer.Serialize(payload, JsonHelpers.SerializerOptions));
        }
    }

    private static AppLogLevel ParseLevel(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "debug" => AppLogLevel.Debug,
            "warn" => AppLogLevel.Warn,
            "error" => AppLogLevel.Error,
            _ => AppLogLevel.Info,
        };
    }
}
