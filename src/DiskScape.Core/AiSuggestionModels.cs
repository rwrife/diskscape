namespace DiskScape.Core;

public enum AiSuggestionMode
{
    Disabled,
    Fallback,
    Generated
}

public enum AiSuggestionRisk
{
    LikelySafeToClean,
    BeCareful
}

public sealed record AiFolderSuggestion(int Rank, string FolderName, AiSuggestionRisk Risk, string Rationale);

public sealed class AiSuggestionResult
{
    private AiSuggestionResult(
        AiSuggestionMode mode,
        string notice,
        string summary,
        IReadOnlyList<AiFolderSuggestion> suggestions)
    {
        Mode = mode;
        Notice = notice;
        Summary = summary;
        Suggestions = suggestions;
    }

    public AiSuggestionMode Mode { get; }
    public string Notice { get; }
    public string Summary { get; }
    public IReadOnlyList<AiFolderSuggestion> Suggestions { get; }

    public static AiSuggestionResult Disabled(string notice) =>
        new(AiSuggestionMode.Disabled, notice, string.Empty, Array.Empty<AiFolderSuggestion>());

    public static AiSuggestionResult Fallback(string notice) =>
        new(AiSuggestionMode.Fallback, notice, string.Empty, Array.Empty<AiFolderSuggestion>());

    public static AiSuggestionResult Generated(string notice, string summary, IReadOnlyList<AiFolderSuggestion> suggestions) =>
        new(AiSuggestionMode.Generated, notice, summary, suggestions);
}

public sealed record AiEndpointProbeResult(bool IsReachable, string Message)
{
    public static AiEndpointProbeResult Reachable(string message) => new(true, message);

    public static AiEndpointProbeResult Unreachable(string message) => new(false, message);
}

public interface IAiSuggester
{
    Task<AiEndpointProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    Task<AiSuggestionResult> SuggestCleanupAsync(ScanNode root, CancellationToken cancellationToken = default);
}

public sealed record LocalAiSettings
{
    public bool Enabled { get; init; }
    public string EndpointUrl { get; init; } = "http://127.0.0.1:11434";
    public string Model { get; init; } = "llama3.2:3b";
    public int MaxFolders { get; init; } = 12;
    public int MaxExtensionsPerFolder { get; init; } = 8;
    public double Temperature { get; init; } = 0.1;
}
