namespace WindowCards.App.Updates;

public sealed record UpdateInfo(
    Version CurrentVersion,
    Version LatestVersion,
    string ReleaseName,
    string ReleaseBody,
    string ReleaseUrl,
    string AssetDownloadUrl,
    long AssetSize);

public enum UpdateCheckOutcome
{
    UpToDate,
    NewerAvailable,
    NoReleaseFound,
    NetworkError
}

public sealed record UpdateCheckResult(
    UpdateCheckOutcome Outcome,
    UpdateInfo? Info,
    string? ErrorMessage);
