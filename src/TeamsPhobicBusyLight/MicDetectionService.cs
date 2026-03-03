using Microsoft.Win32;

namespace TeamsPhobicBusyLight;

public class MicDetectionService
{
    private static readonly string[] ConsentStorePaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone",
        @"CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone",
    ];

    public string? LastActiveApp { get; private set; }

    public bool IsMicrophoneInUse()
    {
        LastActiveApp = null;

        // Check HKLM (system-wide apps)
        if (CheckRegistryHive(Registry.LocalMachine, ConsentStorePaths[0]))
            return true;

        // Check HKCU (per-user apps, including MSIX/Store apps)
        if (CheckRegistryHive(Registry.CurrentUser, ConsentStorePaths[1].Replace("CURRENT_USER\\", "")))
            return true;

        return false;
    }

    private bool CheckRegistryHive(RegistryKey hive, string basePath)
    {
        try
        {
            using var baseKey = hive.OpenSubKey(basePath);
            if (baseKey is null) return false;

            foreach (var subKeyName in baseKey.GetSubKeyNames())
            {
                using var appKey = baseKey.OpenSubKey(subKeyName);
                if (appKey is null) continue;

                // Direct app entry (desktop apps)
                if (IsAppUsingMic(appKey, subKeyName))
                    return true;

                // Nested entries (packaged/MSIX apps like new Teams)
                foreach (var nestedName in appKey.GetSubKeyNames())
                {
                    using var nestedKey = appKey.OpenSubKey(nestedName);
                    if (nestedKey is null) continue;
                    if (IsAppUsingMic(nestedKey, nestedName))
                        return true;
                }
            }
        }
        catch { }

        return false;
    }

    private bool IsAppUsingMic(RegistryKey key, string appName)
    {
        var lastUsedStop = key.GetValue("LastUsedTimeStop");
        if (lastUsedStop is long stopValue && stopValue == 0)
        {
            // LastUsedTimeStop == 0 means the mic is currently in use by this app
            LastActiveApp = appName;
            return true;
        }
        return false;
    }
}
