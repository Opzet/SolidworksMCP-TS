namespace SolidworksMCP;

using System.Text.Json;
using System.Text.Json.Nodes;

internal static class JsonHelpers
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static Dictionary<string, object?> ToDictionary(this JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = FromJsonElement(property.Value);
        }

        return dictionary;
    }

    public static object? FromJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => FromJsonElement(property.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(FromJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        };
    }

    public static JsonElement ToJsonElement(object? value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, SerializerOptions));
        return document.RootElement.Clone();
    }

    public static JsonNode? ToJsonNode(object? value)
    {
        return value is null ? null : JsonNode.Parse(JsonSerializer.Serialize(value, SerializerOptions));
    }

    public static Dictionary<string, object?>? AsDictionary(object? value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object?> dictionary => NormalizeDictionary(dictionary),
            JsonElement element when element.ValueKind == JsonValueKind.Object => element.ToDictionary(),
            JsonNode node when node is JsonObject jsonObject => jsonObject.ToDictionary(property => property.Key, property => NormalizeValue(property.Value), StringComparer.OrdinalIgnoreCase),
            _ => null,
        };
    }

    public static List<object?>? AsList(object? value)
    {
        return value switch
        {
            null => null,
            IEnumerable<object?> enumerable => enumerable.Select(NormalizeValue).ToList(),
            JsonElement element when element.ValueKind == JsonValueKind.Array => element.EnumerateArray().Select(FromJsonElement).ToList(),
            JsonNode node when node is JsonArray jsonArray => jsonArray.Select(NormalizeValue).ToList(),
            _ => null,
        };
    }

    public static object? NormalizeValue(object? value)
    {
        return value switch
        {
            JsonElement element => FromJsonElement(element),
            JsonNode node => node.Deserialize<object?>(SerializerOptions),
            Dictionary<string, object?> dictionary => NormalizeDictionary(dictionary),
            IEnumerable<object?> list => list.Select(NormalizeValue).ToList(),
            _ => value,
        };
    }

    public static Dictionary<string, object?> NormalizeDictionary(IDictionary<string, object?> dictionary)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in dictionary)
        {
            result[pair.Key] = NormalizeValue(pair.Value);
        }

        return result;
    }
}
