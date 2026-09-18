namespace SolidworksMCP;

using System.Text.Json;
using System.Text.Json.Nodes;

internal static class ToolHelpers
{
    public static Dictionary<string, object?> ToArguments(JsonElement? arguments)
    {
        return arguments is { ValueKind: JsonValueKind.Object } element ? element.ToDictionary() : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public static string GetString(IDictionary<string, object?> arguments, string key, string defaultValue = "")
    {
        return arguments.TryGetValue(key, out var value) ? Convert.ToString(value) ?? defaultValue : defaultValue;
    }

    public static double GetDouble(IDictionary<string, object?> arguments, string key, double defaultValue = 0)
    {
        return arguments.TryGetValue(key, out var value) && double.TryParse(Convert.ToString(value), out var parsed) ? parsed : defaultValue;
    }

    public static bool GetBool(IDictionary<string, object?> arguments, string key, bool defaultValue = false)
    {
        return arguments.TryGetValue(key, out var value) ? Convert.ToBoolean(value) : defaultValue;
    }

    public static List<string> GetStringList(IDictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value))
        {
            return [];
        }

        return value switch
        {
            IEnumerable<object?> enumerable => enumerable.Select(Convert.ToString).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToList(),
            JsonElement element when element.ValueKind == JsonValueKind.Array => element.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToList(),
            _ => [],
        };
    }

    public static Dictionary<string, object?> GetDictionary(IDictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var dictionary = JsonHelpers.AsDictionary(value);
        return dictionary ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public static SelectionSpec GetSelection(IDictionary<string, object?> arguments, string key = "selection")
    {
        var selection = GetDictionary(arguments, key);
        return new SelectionSpec
        {
            Faces = GetIntList(selection, "faces"),
            Edges = GetIntList(selection, "edges"),
            Vertices = GetIntList(selection, "vertices"),
            Planes = GetStringList(selection, "planes"),
            Axes = GetStringList(selection, "axes"),
            Sketches = GetStringList(selection, "sketches"),
            SketchSegments = GetIntList(selection, "sketch_segments"),
            SketchPoints = GetIntList(selection, "sketch_points"),
            SketchName = GetString(selection, "sketch_name"),
            Features = GetStringList(selection, "features"),
            Components = GetStringList(selection, "components"),
        };
    }

    public static List<int> GetIntList(IDictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value))
        {
            return [];
        }

        return value switch
        {
            IEnumerable<object?> enumerable => enumerable.Select(ConvertToInt32).ToList(),
            JsonElement element when element.ValueKind == JsonValueKind.Array => element.EnumerateArray().Select(item => ConvertToInt32(item)).ToList(),
            _ => [],
        };
    }

    private static int ConvertToInt32(object? value)
        => value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetInt32(),
            JsonElement element when element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed) => parsed,
            _ => Convert.ToInt32(value),
        };

    public static object SuccessText(string text) => text;

    public static object SuccessObject(Dictionary<string, object?> data) => data;

    public static object Failure(string message) => message;
}
