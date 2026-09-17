namespace SolidworksMCP;

using System.Text.Json.Nodes;

internal static class JsonSchemaBuilder
{
    public static JsonObject Object()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
            ["additionalProperties"] = false,
        };
    }

    public static JsonObject Object(params (string Name, JsonNode Schema)[] properties)
    {
        var root = Object();

        var props = (JsonObject)root["properties"]!;
        foreach (var (name, schema) in properties)
        {
            props[name] = schema;
        }

        return root;
    }

    public static JsonObject ObjectWithRequired(string[] required, params (string Name, JsonNode Schema)[] properties)
    {
        var root = Object(properties);
        var requiredArray = new JsonArray();

        foreach (var item in required)
        {
            requiredArray.Add(item);
        }

        root["required"] = requiredArray;
        return root;
    }

    public static JsonObject String(string? description = null, string? @default = null)
    {
        var node = new JsonObject { ["type"] = "string" };
        if (!string.IsNullOrWhiteSpace(description))
        {
            node["description"] = description;
        }

        if (@default is not null)
        {
            node["default"] = @default;
        }

        return node;
    }

    public static JsonObject Number(string? description = null, double? @default = null)
    {
        var node = new JsonObject { ["type"] = "number" };
        if (!string.IsNullOrWhiteSpace(description))
        {
            node["description"] = description;
        }

        if (@default.HasValue)
        {
            node["default"] = @default.Value;
        }

        return node;
    }

    public static JsonObject Integer(string? description = null, int? @default = null)
    {
        var node = new JsonObject { ["type"] = "integer" };
        if (!string.IsNullOrWhiteSpace(description))
        {
            node["description"] = description;
        }

        if (@default.HasValue)
        {
            node["default"] = @default.Value;
        }

        return node;
    }

    public static JsonObject Boolean(string? description = null, bool? defaultValue = null)
    {
        var node = new JsonObject { ["type"] = "boolean" };
        if (!string.IsNullOrWhiteSpace(description))
        {
            node["description"] = description;
        }

        if (defaultValue.HasValue)
        {
            node["default"] = defaultValue.Value;
        }

        return node;
    }

    public static JsonObject Any(string? description = null)
    {
        var node = new JsonObject();
        if (!string.IsNullOrWhiteSpace(description))
        {
            node["description"] = description;
        }

        return node;
    }

    public static JsonObject Enum(IEnumerable<string> values, string? description = null, string? @default = null)
    {
        var node = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = new JsonArray(values.Select(value => JsonValue.Create(value)).ToArray()),
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            node["description"] = description;
        }

        if (@default is not null)
        {
            node["default"] = @default;
        }

        return node;
    }

    public static JsonObject Array(JsonNode itemSchema, string? description = null)
    {
        var node = new JsonObject
        {
            ["type"] = "array",
            ["items"] = itemSchema,
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            node["description"] = description;
        }

        return node;
    }
}
