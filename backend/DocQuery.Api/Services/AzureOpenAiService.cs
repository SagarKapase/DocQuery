using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DocQuery.Api.Services;

// Talks to the AI provider with plain HTTP calls (no SDK), so every request is visible.
// OpenRouter, OpenAI and Azure OpenAI all use the same request/response format;
// only the URL and the key header differ.
// "AiProvider" in appsettings.json picks which one: "OpenRouter", "OpenAI" or "AzureOpenAI".
public class AzureOpenAiService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AzureOpenAiService> _logger;
    private readonly bool _useAzure;
    private readonly IConfigurationSection _settings; // the section of the chosen provider

    public AzureOpenAiService(HttpClient http, IConfiguration config, ILogger<AzureOpenAiService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _useAzure = config["AiProvider"] == "AzureOpenAI";
        _settings = config.GetSection(config["AiProvider"]!);

        if (_useAzure)
            _http.DefaultRequestHeaders.Add("api-key", _settings["ApiKey"]);
        else
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings["ApiKey"]}");
    }

    // operation = "embeddings" or "chat/completions"
    private string Url(string operation, string azureDeployment)
    {
        if (!_useAzure) return $"{_settings["BaseUrl"]!.TrimEnd('/')}/{operation}";

        var endpoint = _settings["Endpoint"]!.TrimEnd('/');
        var version = _settings["ApiVersion"];
        return $"{endpoint}/openai/deployments/{azureDeployment}/{operation}?api-version={version}";
    }

    private string EmbeddingModel => _useAzure ? _settings["EmbeddingDeployment"]! : _settings["EmbeddingModel"]!;
    private string ChatModel => _useAzure ? _settings["ChatDeployment"]! : _settings["ChatModel"]!;

    // Turns texts into vectors (one float[] per text).
    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
    {
        var url = Url("embeddings", EmbeddingModel);
        var response = await _http.PostAsJsonAsync(url, new { model = EmbeddingModel, input = texts });

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Embedding request failed {Status}: {Error}", response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = new List<float[]>();
        foreach (var item in json.RootElement.GetProperty("data").EnumerateArray())
        {
            var vector = item.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray();
            result.Add(vector);
        }
        return result;
    }

    // Asks the chat model and yields the answer piece by piece as it is generated.
    public async IAsyncEnumerable<string> StreamChatAsync(
        string systemPrompt, string userPrompt, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = Url("chat/completions", ChatModel);
        var body = new
        {
            model = ChatModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            stream = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        // ResponseHeadersRead = don't wait for the full body, start reading immediately
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Chat request failed {Status}: {Error}", response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // Azure sends lines like:  data: {"choices":[{"delta":{"content":"Hel"}}]}
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var data = line.Substring(6);
            if (data == "[DONE]") break;

            using var json = JsonDocument.Parse(data);
            var choices = json.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) continue; // first message is only content-filter info

            if (choices[0].GetProperty("delta").TryGetProperty("content", out var content))
            {
                var text = content.GetString();
                if (!string.IsNullOrEmpty(text)) yield return text;
            }
        }
    }
}
