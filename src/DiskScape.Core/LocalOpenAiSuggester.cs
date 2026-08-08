using System.Text;
using System.Text.Json;

namespace DiskScape.Core;

public sealed class LocalOpenAiSuggester : IAiSuggester
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly LocalAiSettings _settings;

    public LocalOpenAiSuggester(HttpClient httpClient, LocalAiSettings? settings = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? new LocalAiSettings();
    }

    public async Task<AiEndpointProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return AiEndpointProbeResult.Unreachable("Local AI suggestions are disabled by settings.");
        }

        try
        {
            using var response = await _httpClient.GetAsync(BuildApiUri("models"), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return AiEndpointProbeResult.Reachable("Local AI endpoint is reachable.");
            }

            return AiEndpointProbeResult.Unreachable($"Local AI endpoint responded with HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return AiEndpointProbeResult.Unreachable($"Local AI endpoint is unreachable: {ex.Message}");
        }
    }

    public async Task<AiSuggestionResult> SuggestCleanupAsync(ScanNode root, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!_settings.Enabled)
        {
            return AiSuggestionResult.Disabled("Local AI suggestions are off by default. Enable them in settings to use this feature.");
        }

        var probe = await ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (!probe.IsReachable)
        {
            return AiSuggestionResult.Fallback($"Continuing in non-AI mode. {probe.Message}");
        }

        try
        {
            var snapshot = AiSuggestionRequestBuilder.BuildSnapshot(root, _settings.MaxFolders, _settings.MaxExtensionsPerFolder);
            var requestPayload = BuildChatRequest(snapshot);
            var requestJson = JsonSerializer.Serialize(requestPayload, JsonSerializerOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildApiUri("chat/completions"))
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return AiSuggestionResult.Fallback(
                    $"Continuing in non-AI mode. Local endpoint returned HTTP {(int)response.StatusCode}: {responseJson}");
            }

            if (!TryExtractAssistantContent(responseJson, out var modelContent))
            {
                return AiSuggestionResult.Fallback("Continuing in non-AI mode. Endpoint response did not contain assistant output.");
            }

            if (!TryParseModelSuggestions(modelContent, out var summary, out var suggestions))
            {
                var plainSummary = modelContent.Trim();
                if (plainSummary.Length == 0)
                {
                    return AiSuggestionResult.Fallback("Continuing in non-AI mode. Endpoint returned an empty model response.");
                }

                return AiSuggestionResult.Generated(
                    notice: "Local AI returned non-JSON output; showing raw summary text.",
                    summary: plainSummary,
                    suggestions: Array.Empty<AiFolderSuggestion>());
            }

            return AiSuggestionResult.Generated(
                notice: "Local AI suggestions generated successfully.",
                summary: summary,
                suggestions: suggestions);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            return AiSuggestionResult.Fallback($"Continuing in non-AI mode. Local AI request failed: {ex.Message}");
        }
    }

    private object BuildChatRequest(AiScanSnapshot snapshot)
    {
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonSerializerOptions);

        return new
        {
            model = _settings.Model,
            temperature = _settings.Temperature,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You classify disk folders for cleanup safety. Return strict JSON with fields: summary (string), suggestions (array). Each suggestion item must include folderName (string), risk (safe_to_clean|be_careful), rationale (string)."
                },
                new
                {
                    role = "user",
                    content = $"Analyze this scan snapshot and return ranked cleanup guidance as JSON only:\n{snapshotJson}"
                }
            }
        };
    }

    private Uri BuildApiUri(string relativePath)
    {
        var endpoint = _settings.EndpointUrl.Trim().TrimEnd('/');
        var v1Base = endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? endpoint
            : $"{endpoint}/v1";

        return new Uri($"{v1Base}/{relativePath.TrimStart('/')}", UriKind.Absolute);
    }

    private static bool TryExtractAssistantContent(string responseJson, out string content)
    {
        content = string.Empty;

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return false;
        }

        var first = choices[0];
        if (!first.TryGetProperty("message", out var message))
        {
            return false;
        }

        if (!message.TryGetProperty("content", out var contentElement))
        {
            return false;
        }

        content = contentElement.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryParseModelSuggestions(
        string modelContent,
        out string summary,
        out IReadOnlyList<AiFolderSuggestion> suggestions)
    {
        summary = string.Empty;
        suggestions = Array.Empty<AiFolderSuggestion>();

        var normalizedJson = StripCodeFences(modelContent);

        using var doc = JsonDocument.Parse(normalizedJson);
        var root = doc.RootElement;

        summary = root.TryGetProperty("summary", out var summaryElement)
            ? summaryElement.GetString() ?? string.Empty
            : string.Empty;

        if (!root.TryGetProperty("suggestions", out var suggestionsElement) || suggestionsElement.ValueKind != JsonValueKind.Array)
        {
            return summary.Length > 0;
        }

        var ranked = new List<AiFolderSuggestion>();
        var rank = 1;

        foreach (var item in suggestionsElement.EnumerateArray())
        {
            var folderName = item.TryGetProperty("folderName", out var folderNameElement)
                ? folderNameElement.GetString() ?? string.Empty
                : string.Empty;

            var riskText = item.TryGetProperty("risk", out var riskElement)
                ? riskElement.GetString() ?? string.Empty
                : string.Empty;

            var rationale = item.TryGetProperty("rationale", out var rationaleElement)
                ? rationaleElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(folderName))
            {
                continue;
            }

            ranked.Add(new AiFolderSuggestion(
                Rank: rank,
                FolderName: folderName,
                Risk: ParseRisk(riskText),
                Rationale: rationale));

            rank++;
        }

        suggestions = ranked;
        return summary.Length > 0 || ranked.Count > 0;
    }

    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        var trailingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (trailingFence <= firstNewline)
        {
            return trimmed[(firstNewline + 1)..].Trim();
        }

        return trimmed[(firstNewline + 1)..trailingFence].Trim();
    }

    private static AiSuggestionRisk ParseRisk(string riskText)
    {
        return riskText.Trim().ToLowerInvariant() switch
        {
            "safe_to_clean" => AiSuggestionRisk.LikelySafeToClean,
            "likely_safe_to_clean" => AiSuggestionRisk.LikelySafeToClean,
            "be_careful" => AiSuggestionRisk.BeCareful,
            "risky" => AiSuggestionRisk.BeCareful,
            _ => AiSuggestionRisk.BeCareful
        };
    }
}
