using System.Diagnostics;
using System.IO.Ports;

namespace TeamsBusyLight;

public static class ArduinoFlasher
{
    /// <summary>
    /// Flash the BusyLight firmware to an Arduino Pro Micro (ATmega32U4 / Caterina bootloader).
    /// 1. Open serial at 1200 baud and close — this triggers the bootloader reset.
    /// 2. Wait for the bootloader COM port to appear.
    /// 3. Run avrdude to upload the hex.
    /// </summary>
    public static async Task<(bool Success, string Output)> FlashAsync(string comPort, Action<string>? onProgress = null)
    {
        var resourceDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        var avrdudePath = Path.Combine(resourceDir, "avrdude.exe");
        var confPath = Path.Combine(resourceDir, "avrdude.conf");
        var hexPath = Path.Combine(resourceDir, "BusyLight.hex");

        if (!File.Exists(avrdudePath))
            return (false, $"avrdude.exe not found at {avrdudePath}");
        if (!File.Exists(hexPath))
            return (false, $"BusyLight.hex not found at {hexPath}");

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
}
