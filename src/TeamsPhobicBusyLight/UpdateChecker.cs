using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TeamsPhobicBusyLight;

public static class UpdateChecker
{
    private const string RepoOwner = "phobicdotno";
    private const string RepoName = "TeamsPhobicBusyLight";
    private static readonly HttpClient Http = new();

    static UpdateChecker()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("TeamsPhobicBusyLight");
    }

    public static string CurrentVersion
    {
        get
        {
            var asm = typeof(UpdateChecker).Assembly;
            var ver = asm.GetName().Version;
            return ver is not null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v0.0.0";
        }
    }

    public static async Task<ReleaseInfo?> CheckForUpdateAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var release = await Http.GetFromJsonAsync<GitHubRelease>(url);
            if (release is null || string.IsNullOrEmpty(release.TagName))
                return null;

            var latestTag = release.TagName.TrimStart('v');
            var currentTag = CurrentVersion.TrimStart('v');

            if (!Version.TryParse(latestTag, out var latest) ||
                !Version.TryParse(currentTag, out var current))
                return null;

            if (latest <= current)
                return null;

            // Find asset URLs
            string? exeUrl = null;
            string? hexUrl = null;
            string? msiUrl = null;

            foreach (var asset in release.Assets ?? [])
            {
                var name = asset.Name?.ToLowerInvariant() ?? "";
                if (name.EndsWith(".msi"))
                    msiUrl = asset.BrowserDownloadUrl;
                else if (name.EndsWith(".exe") && name.Contains("busylight", StringComparison.OrdinalIgnoreCase))
                    exeUrl = asset.BrowserDownloadUrl;
                else if (name.EndsWith(".hex"))
                    hexUrl = asset.BrowserDownloadUrl;
            }

            return new ReleaseInfo
            {
                TagName = release.TagName,
                HtmlUrl = release.HtmlUrl ?? "",
                Body = release.Body ?? "",
                MsiUrl = msiUrl,
                ExeUrl = exeUrl,
                HexUrl = hexUrl
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> DownloadHexUpdateAsync(string hexUrl, Action<string>? onProgress = null)
    {
        try
        {
            onProgress?.Invoke("Downloading new firmware...");
            var hexBytes = await Http.GetByteArrayAsync(hexUrl);

            var hexPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeamsPhobicBusyLight", "avrdude", "BusyLight.hex");
            Directory.CreateDirectory(Path.GetDirectoryName(hexPath)!);
            await File.WriteAllBytesAsync(hexPath, hexBytes);

            onProgress?.Invoke("Firmware updated! Flash Arduino to apply.");
            return true;
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"Download failed: {ex.Message}");
            return false;
        }
    }

    public static async Task SelfUpdateAsync(string exeUrl)
    {
        var tempExe = Path.Combine(Path.GetTempPath(), "TeamsPhobicBusyLight_update.exe");
        var updateScript = Path.Combine(Path.GetTempPath(), "TeamsPhobicBusyLight_update.cmd");
        var currentExe = Environment.ProcessPath!;

        // Download new exe
        var exeBytes = await Http.GetByteArrayAsync(exeUrl);
        await File.WriteAllBytesAsync(tempExe, exeBytes);

        // Write batch script that replaces the exe and relaunches
        var script = $"""
            @echo off
            timeout /t 2 /nobreak >nul
            copy /y "{tempExe}" "{currentExe}" >nul
            del "{tempExe}" >nul
            start "" "{currentExe}"
            del "%~f0" >nul
            """;
        await File.WriteAllTextAsync(updateScript, script);

        // Launch the updater script and exit
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{updateScript}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Environment.Exit(0);
    }

    public static void OpenReleasePage(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}

public class ReleaseInfo
{
    public string TagName { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public string Body { get; set; } = "";
    public string? MsiUrl { get; set; }
    public string? ExeUrl { get; set; }
    public string? HexUrl { get; set; }
}

file record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("html_url")] string? HtmlUrl,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("assets")] GitHubAsset[]? Assets
);

file record GitHubAsset(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl
);
