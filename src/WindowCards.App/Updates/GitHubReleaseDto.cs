using System.Text.Json.Serialization;

namespace WindowCards.App.Updates;

internal sealed record GitHubReleaseDto(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("prerelease")] bool Prerelease,
    [property: JsonPropertyName("assets")] GitHubAssetDto[]? Assets);

internal sealed record GitHubAssetDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
