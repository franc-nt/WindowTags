using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;

namespace WindowCards.App.Updates;

public static class UpdateChecker
{
    private const string LatestUrl =
        "https://api.github.com/repos/franc-nt/WindowTags/releases/latest";
    private const string AssetName = "WindowCards.exe";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WindowCards-Updater/1.0");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static Version GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        return Normalize(v);
    }

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var current = GetCurrentVersion();
        try
        {
            using var resp = await Http.GetAsync(LatestUrl, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return new UpdateCheckResult(UpdateCheckOutcome.NoReleaseFound, null, null);
            resp.EnsureSuccessStatusCode();

            var dto = await resp.Content.ReadFromJsonAsync<GitHubReleaseDto>(cancellationToken: ct)
                .ConfigureAwait(false);
            if (dto is null || dto.Draft)
                return new UpdateCheckResult(UpdateCheckOutcome.NoReleaseFound, null, null);

            if (!TryParseTag(dto.TagName, out var latest))
                return new UpdateCheckResult(UpdateCheckOutcome.NoReleaseFound, null, null);

            var asset = dto.Assets?.FirstOrDefault(a =>
                a.Name.Equals(AssetName, StringComparison.OrdinalIgnoreCase));
            if (asset is null)
                return new UpdateCheckResult(UpdateCheckOutcome.NoReleaseFound, null, null);

            var info = new UpdateInfo(
                current, latest,
                dto.Name ?? dto.TagName,
                dto.Body ?? string.Empty,
                dto.HtmlUrl,
                asset.BrowserDownloadUrl,
                asset.Size);

            return latest > current
                ? new UpdateCheckResult(UpdateCheckOutcome.NewerAvailable, info, null)
                : new UpdateCheckResult(UpdateCheckOutcome.UpToDate, info, null);
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.NetworkError, null, "Tempo de conexão esgotado.");
        }
        catch (HttpRequestException ex)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.NetworkError, null, ex.Message);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.NetworkError, null, ex.Message);
        }
    }

    internal static bool TryParseTag(string tag, out Version version)
    {
        var s = tag;
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s[1..];
        if (!Version.TryParse(s, out var raw))
        {
            version = new Version(0, 0, 0, 0);
            return false;
        }
        version = Normalize(raw);
        return true;
    }

    private static Version Normalize(Version v)
        => new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
}
