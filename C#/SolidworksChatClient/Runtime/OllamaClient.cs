namespace SolidworksChatClient.Runtime;

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class OllamaClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(10),
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<OllamaChatResponse> ChatAsync(string baseUrl, string model, IReadOnlyList<OllamaMessage> messages, JsonArray tools, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(tools);
        ValidateEndpointInputs(baseUrl, model);

        var endpoint = BuildUri(baseUrl, "api/chat");
        var request = new OllamaChatRequest(model, messages, tools, Stream: false);

        try
        {
            using var response = await Http.PostAsJsonAsync(endpoint, request, SerializerOptions, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, endpoint, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(SerializerOptions, cancellationToken).ConfigureAwait(false);
            return payload ?? throw new InvalidOperationException("Ollama returned an empty chat response.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw BuildTimeoutException(endpoint, ex);
        }
        catch (HttpRequestException ex)
        {
            throw BuildConnectionException(endpoint, ex);
        }
    }

    public static async Task<string> CompleteAsync(string baseUrl, string model, string prompt, CancellationToken cancellationToken = default)
    {
        ValidateEndpointInputs(baseUrl, model);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }

        var endpoint = BuildUri(baseUrl, "api/generate");
        var request = new JsonObject
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["stream"] = false,
        };

        try
        {
            using var response = await Http.PostAsJsonAsync(endpoint, request, SerializerOptions, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, endpoint, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content.ReadFromJsonAsync<JsonObject>(SerializerOptions, cancellationToken).ConfigureAwait(false) ?? new JsonObject();
            var text = payload["response"]?.GetValue<string>();
            return !string.IsNullOrWhiteSpace(text)
                ? text
                : throw new InvalidOperationException("Ollama generate response did not contain text.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw BuildTimeoutException(endpoint, ex);
        }
        catch (HttpRequestException ex)
        {
            throw BuildConnectionException(endpoint, ex);
        }
    }

    public static async Task<string> CompleteStreamingAsync(string baseUrl, string model, string prompt, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateEndpointInputs(baseUrl, model);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }

        var endpoint = BuildUri(baseUrl, "api/generate");
        var requestPayload = new JsonObject
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["stream"] = true,
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(requestPayload.ToJsonString(SerializerOptions), Encoding.UTF8, "application/json"),
            };

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, endpoint, cancellationToken).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var fullResponse = new StringBuilder();

            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var node = JsonNode.Parse(line) as JsonObject;
                var chunk = node?["response"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(chunk))
                {
                    progress?.Report(chunk);
                    fullResponse.Append(chunk);
                }

                if (node?["done"]?.GetValue<bool>() == true)
                {
                    break;
                }
            }

            return fullResponse.Length > 0
                ? fullResponse.ToString()
                : throw new InvalidOperationException("Ollama streaming generate returned no text chunks.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw BuildTimeoutException(endpoint, ex);
        }
        catch (HttpRequestException ex)
        {
            throw BuildConnectionException(endpoint, ex);
        }
    }

    public static async Task<float[]> EmbedAsync(string baseUrl, string embeddingModel, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Ollama base URL is required.", nameof(baseUrl));
        }

        if (string.IsNullOrWhiteSpace(embeddingModel))
        {
            throw new ArgumentException("Embedding model is required.", nameof(embeddingModel));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var endpoint = BuildUri(baseUrl, "api/embeddings");
        var request = new JsonObject
        {
            ["model"] = embeddingModel,
            ["prompt"] = text,
        };

        try
        {
            using var response = await Http.PostAsJsonAsync(endpoint, request, SerializerOptions, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, endpoint, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content.ReadFromJsonAsync<JsonObject>(SerializerOptions, cancellationToken).ConfigureAwait(false) ?? new JsonObject();
            return ExtractEmbedding(payload);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw BuildTimeoutException(endpoint, ex);
        }
        catch (HttpRequestException ex)
        {
            throw BuildConnectionException(endpoint, ex);
        }
    }

    public static async Task<OllamaConnectionStatus> TestConnectionAsync(string baseUrl, string model, CancellationToken cancellationToken = default)
    {
        ValidateEndpointInputs(baseUrl, model);
        var endpoint = BuildUri(baseUrl, "api/tags");

        try
        {
            using var response = await Http.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, endpoint, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content.ReadFromJsonAsync<JsonObject>(SerializerOptions, cancellationToken).ConfigureAwait(false) ?? new JsonObject();
            var availableModels = ExtractModelNames(payload);

            var isModelAvailable = false;
            foreach (var availableModel in availableModels)
            {
                if (string.Equals(availableModel, model, StringComparison.OrdinalIgnoreCase))
                {
                    isModelAvailable = true;
                    break;
                }
            }

            return new OllamaConnectionStatus(true, isModelAvailable, availableModels.Count, availableModels);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw BuildTimeoutException(endpoint, ex);
        }
        catch (HttpRequestException ex)
        {
            throw BuildConnectionException(endpoint, ex);
        }
    }

    private static void ValidateEndpointInputs(string baseUrl, string model)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Ollama base URL is required.", nameof(baseUrl));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Ollama model is required.", nameof(model));
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, Uri endpoint, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException($"Ollama request to '{endpoint}' failed with {(int)response.StatusCode} ({response.StatusCode}). Response: {Truncate(responseBody, 300)}");
    }

    private static InvalidOperationException BuildTimeoutException(Uri endpoint, TaskCanceledException exception)
        => new($"Ollama request to '{endpoint}' timed out.", exception);

    private static InvalidOperationException BuildConnectionException(Uri endpoint, HttpRequestException exception)
        => new($"Unable to reach Ollama endpoint '{endpoint}': {exception.Message}", exception);

    private static Uri BuildUri(string baseUrl, string relativePath)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/') + '/', UriKind.Absolute);
        return new Uri(baseUri, relativePath);
    }

    private static List<string> ExtractModelNames(JsonObject payload)
    {
        var models = new List<string>();
        if (payload["models"] is not JsonArray modelArray)
        {
            return models;
        }

        foreach (var modelNode in modelArray)
        {
            if (modelNode is not JsonObject modelObject)
            {
                continue;
            }

            var modelName = modelObject["name"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(modelName))
            {
                models.Add(modelName);
            }
        }

        return models;
    }

    private static float[] ExtractEmbedding(JsonObject payload)
    {
        if (payload["embedding"] is not JsonArray embeddingArray)
        {
            return [];
        }

        var values = new float[embeddingArray.Count];
        for (var index = 0; index < embeddingArray.Count; index++)
        {
            if (embeddingArray[index] is JsonValue jsonValue && jsonValue.TryGetValue(out float value))
            {
                values[index] = value;
                continue;
            }

            if (embeddingArray[index] is JsonValue doubleValue && doubleValue.TryGetValue(out double asDouble))
            {
                values[index] = (float)asDouble;
            }
        }

        return values;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
