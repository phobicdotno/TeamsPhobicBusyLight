using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;

namespace TeamsBusyLight;

public static class ArduinoFlasher
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamsBusyLight", "avrdude");

    /// <summary>
    /// Flash the BusyLight firmware to an Arduino Pro Micro (ATmega32U4 / Caterina bootloader).
    /// 1. Extract avrdude from embedded resources (cached).
    /// 2. Open serial at 1200 baud and close — this triggers the bootloader reset.
    /// 3. Wait for the bootloader COM port to appear.
    /// 4. Run avrdude to upload the hex.
    /// </summary>
    public static async Task<(bool Success, string Output)> FlashAsync(string comPort, Action<string>? onProgress = null)
    {
        var avrdudePath = Path.Combine(CacheDir, "avrdude.exe");
        var confPath = Path.Combine(CacheDir, "avrdude.conf");
        var hexPath = Path.Combine(CacheDir, "BusyLight.hex");

        // Extract embedded resources if not already cached
        try
        {
            ExtractIfMissing("avrdude.exe", avrdudePath);
            ExtractIfMissing("avrdude.conf", confPath);
            ExtractIfMissing("BusyLight.hex", hexPath);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to extract resources: {ex.Message}");
        }

        onProgress?.Invoke("Triggering bootloader reset (1200 baud touch)...");

        // Step 1: 1200 baud touch to enter bootloader
        try
        {
            using var port = new SerialPort(comPort, 1200);
            port.DtrEnable = true;
            port.Open();
            await Task.Delay(100);
            port.Close();
        }
        catch (Exception ex)
        {
            return (false, $"Failed to reset Arduino: {ex.Message}");
        }

        // Step 2: Wait for bootloader port (may change COM port number)
        onProgress?.Invoke("Waiting for bootloader...");
        string? bootloaderPort = null;
        var originalPorts = new HashSet<string>(SerialPort.GetPortNames());

        for (int i = 0; i < 40; i++) // up to 8 seconds
        {
            await Task.Delay(200);
            var currentPorts = SerialPort.GetPortNames();
            // Look for a new port (bootloader port)
            var newPort = currentPorts.FirstOrDefault(p => !originalPorts.Contains(p));
            if (newPort is not null)
            {
                bootloaderPort = newPort;
                break;
            }
            // Or the original port may have reappeared
            if (i > 5 && currentPorts.Contains(comPort))
            {
                bootloaderPort = comPort;
                break;
            }
        }

        bootloaderPort ??= comPort; // fallback to original
        onProgress?.Invoke($"Flashing on {bootloaderPort}...");

        // Step 3: Run avrdude
        var args = $"-C \"{confPath}\" -p atmega32u4 -c avr109 -P {bootloaderPort} -U flash:w:\"{hexPath}\":i";

        var psi = new ProcessStartInfo
        {
            FileName = avrdudePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = (stdout + "\n" + stderr).Trim();

            if (process.ExitCode == 0)
            {
                onProgress?.Invoke("Flash complete!");
                return (true, output);
            }
            else
            {
                return (false, $"avrdude exited with code {process.ExitCode}:\n{output}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to run avrdude: {ex.Message}");
        }
    }

    private static void ExtractIfMissing(string resourceFileName, string targetPath)
    {
        if (File.Exists(targetPath)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Embedded resource '{resourceFileName}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var fs = File.Create(targetPath);
        stream.CopyTo(fs);
    }
}
