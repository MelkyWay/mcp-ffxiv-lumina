namespace McpLumina.Models.Responses;

public sealed record LanguagesResponse : BaseResponse
{
    public LanguageEntry[] Languages { get; init; } = [];
}

public sealed record LanguageEntry(
    string Code,         // "en", "fr", "de", "ja"
    string DisplayName,  // "English", "French", "German", "Japanese"
    bool   Available     // detected on disk for this game installation
);
