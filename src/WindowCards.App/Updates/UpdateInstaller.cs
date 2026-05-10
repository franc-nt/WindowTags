using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace WindowCards.App.Updates;

public sealed record InstallResult(bool Success, string? Error)
{
    public static InstallResult Ok() => new(true, null);
    public static InstallResult Fail(string err) => new(false, err);
}

public static class UpdateInstaller
{
    private const string OldSuffix = ".old";

    public static string GetCurrentExePath()
        => Process.GetCurrentProcess().MainModule!.FileName!;

    public static void CleanupLeftoverOldExe()
    {
        try
        {
            var old = GetCurrentExePath() + OldSuffix;
            if (File.Exists(old)) File.Delete(old);
        }
        catch
        {
            // best-effort; tenta de novo no próximo startup
        }
    }

    public static async Task<InstallResult> InstallAsync(
        UpdateInfo info,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var exePath = GetCurrentExePath();
        var dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(dir))
            return InstallResult.Fail("Não foi possível resolver o diretório do executável atual.");

        var oldPath = exePath + OldSuffix;
        var tempDownload = Path.Combine(dir, $".update-{Guid.NewGuid():N}.tmp");

        try
        {
            if (!IsDirectoryWritable(dir))
                return InstallResult.Fail(
                    $"Sem permissão de escrita em '{dir}'. Mova o WindowCards para um diretório do usuário.");

            await DownloadAsync(info.AssetDownloadUrl, tempDownload, info.AssetSize, progress, ct)
                .ConfigureAwait(false);

            var size = new FileInfo(tempDownload).Length;
            if (info.AssetSize > 0 && size != info.AssetSize)
            {
                TryDelete(tempDownload);
                return InstallResult.Fail(
                    $"Tamanho do download ({size} bytes) difere do esperado ({info.AssetSize} bytes).");
            }

            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); }
                catch { /* fica para o próximo startup */ }
            }

            File.Move(exePath, oldPath);

            try
            {
                File.Move(tempDownload, exePath);
            }
            catch
            {
                try { File.Move(oldPath, exePath); } catch { }
                throw;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            return InstallResult.Ok();
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempDownload);
            return InstallResult.Fail("Atualização cancelada.");
        }
        catch (Exception ex)
        {
            TryDelete(tempDownload);
            return InstallResult.Fail(ex.Message);
        }
    }

    private static async Task DownloadAsync(
        string url, string destination, long expectedSize,
        IProgress<double>? progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WindowCards-Updater/1.0");

        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? expectedSize;
        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = File.Create(destination);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            if (total > 0) progress?.Report((double)read / total);
        }
    }

    private static bool IsDirectoryWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
