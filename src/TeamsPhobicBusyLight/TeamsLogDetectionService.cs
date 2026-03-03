using System.Text.RegularExpressions;

namespace TeamsPhobicBusyLight;

public class TeamsLogDetectionService
{
    private static readonly HashSet<string> BusyStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Busy", "InAMeeting", "DoNotDisturb", "Presenting"
    };

    private static readonly HashSet<string> AvailableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available", "Away", "BeRightBack", "Offline"
    };

    private static readonly Regex AvailabilityRegex = new(
        "\"availability\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.Compiled);

    public string? LastStatus { get; private set; }
    public string? TeamsVersion { get; private set; }
    public bool TeamsFound { get; private set; }
    public string? DetectionInfo { get; private set; }

    /// <summary>
    /// Detect which Teams version is installed and whether log detection will work.
    /// </summary>
    public void DetectTeamsInstallation()
    {
        TeamsFound = false;
        TeamsVersion = null;
        DetectionInfo = null;

        // Check for New Teams (MSIX package)
        var newTeamsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "MSTeams_8wekyb3d8bbwe");

        if (Directory.Exists(newTeamsPath))
        {
            TeamsFound = true;
            TeamsVersion = "New Teams (MSIX)";
            DetectionInfo = "New Teams detected — log file detection supported.";
            return;
        }

        // Check for Classic Teams
        var classicTeamsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Teams");

        if (Directory.Exists(classicTeamsPath))
        {
            TeamsFound = true;
            TeamsVersion = "Classic Teams";
            DetectionInfo = "Classic Teams detected — log file detection not supported (EOL). Use Graph API or Microphone mode.";
            return;
        }

        DetectionInfo = "Teams not found. Install Microsoft Teams or use Microphone mode.";
    }

    /// <summary>
    /// Returns true if the user is in a meeting/busy, false if available, null if unable to determine.
    /// </summary>
    public bool? IsInMeeting()
    {
        var status = ReadCurrentStatus();
        if (status is null) return null;

        LastStatus = status;

        if (BusyStatuses.Contains(status)) return true;
        if (AvailableStatuses.Contains(status)) return false;

        return null;
    }

    private string? ReadCurrentStatus()
    {
        try
        {
            var cacheStoragePath = GetCacheStoragePath();
            if (cacheStoragePath is null) return null;

            // Find the most recently modified file containing availability
            var candidates = Directory.GetDirectories(cacheStoragePath);
            string? latestStatus = null;
            DateTime latestTime = DateTime.MinValue;

            foreach (var hashDir in candidates)
            {
                foreach (var subDir in Directory.GetDirectories(hashDir))
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(subDir))
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.LastWriteTime <= latestTime) continue;

                            var content = ReadFileSafe(file);
                            if (content is null) continue;

                            var match = AvailabilityRegex.Match(content);
                            if (match.Success)
                            {
                                latestStatus = match.Groups[1].Value;
                                latestTime = fileInfo.LastWriteTime;
                            }
                        }
                    }
                    catch { }
                }
            }

            return latestStatus;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCacheStoragePath()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "MSTeams_8wekyb3d8bbwe",
            "LocalCache", "Microsoft", "MSTeams",
            "EBWebView", "WV2Profile_tfw",
            "Service Worker", "CacheStorage");

        if (!Directory.Exists(basePath)) return null;
        return basePath;
    }

    private static string? ReadFileSafe(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            // Only read first 8KB to avoid large files
            var buffer = new char[8192];
            var count = reader.Read(buffer, 0, buffer.Length);
            return new string(buffer, 0, count);
        }
        catch
        {
            return null;
        }
    }
}
