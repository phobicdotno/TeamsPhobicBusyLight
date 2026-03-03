# Teams Busy Light Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build an Arduino sketch + C# system tray app that turns a 12V LED panel on/off based on Microsoft Teams meeting status.

**Architecture:** Arduino Pro Micro listens on USB serial for "1"/"0" commands and drives a relay. A C# .NET 8 WinForms tray app polls Microsoft Graph `/me/presence` every 5 seconds, compares activity to known "in-meeting" states, and sends the appropriate command over serial.

**Tech Stack:** Arduino (C++), C# .NET 8, WinForms (tray icon), MSAL.NET, System.IO.Ports, Microsoft Graph REST API

---

### Task 1: Arduino Sketch

**Files:**
- Create: `arduino/BusyLight/BusyLight.ino`

**Step 1: Create the sketch**

```cpp
const int RELAY_PIN = 9;

void setup() {
  pinMode(RELAY_PIN, OUTPUT);
  digitalWrite(RELAY_PIN, LOW);
  Serial.begin(9600);
}

void loop() {
  if (Serial.available() > 0) {
    char cmd = Serial.read();
    if (cmd == '1') {
      digitalWrite(RELAY_PIN, HIGH);
    } else if (cmd == '0') {
      digitalWrite(RELAY_PIN, LOW);
    }
  }
}
```

**Step 2: Commit**

```bash
git add arduino/BusyLight/BusyLight.ino
git commit -m "feat: Arduino sketch — relay control via serial"
```

---

### Task 2: C# Project Scaffold + Serial Service

**Files:**
- Create: `src/TeamsBusyLight/TeamsBusyLight.csproj`
- Create: `src/TeamsBusyLight/Program.cs`
- Create: `src/TeamsBusyLight/SerialService.cs`
- Create: `src/TeamsBusyLight/.gitignore`

**Step 1: Create the .NET project**

```bash
cd C:\DevOps\TeamsBusyLight\src
dotnet new winforms -n TeamsBusyLight --framework net8.0
```

**Step 2: Add NuGet packages**

```bash
cd src/TeamsBusyLight
dotnet add package System.IO.Ports
dotnet add package Microsoft.Identity.Client --version 4.*
dotnet add package System.Net.Http.Json
```

**Step 3: Create SerialService.cs**

```csharp
using System.IO.Ports;

namespace TeamsBusyLight;

public class SerialService : IDisposable
{
    private SerialPort? _port;

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public bool Open(string portName, int baud = 9600)
    {
        try
        {
            _port = new SerialPort(portName, baud);
            _port.Open();
            return true;
        }
        catch { return false; }
    }

    public void SetLight(bool on)
    {
        if (_port is { IsOpen: true })
            _port.Write(on ? "1" : "0");
    }

    public void Dispose()
    {
        if (_port is { IsOpen: true })
        {
            try { _port.Write("0"); } catch { }
            _port.Close();
        }
        _port?.Dispose();
    }
}
```

**Step 4: Build and verify**

```bash
dotnet build src/TeamsBusyLight
```

Expected: Build succeeded.

**Step 5: Commit**

```bash
git add src/
git commit -m "feat: C# project scaffold + SerialService"
```

---

### Task 3: GraphService — Teams Presence Polling

**Files:**
- Create: `src/TeamsBusyLight/GraphService.cs`

**Step 1: Create GraphService.cs**

```csharp
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TeamsBusyLight;

public class GraphService
{
    private static readonly string[] Scopes = ["Presence.Read"];
    private static readonly HashSet<string> InMeetingActivities = new(StringComparer.OrdinalIgnoreCase)
    {
        "InACall", "InAConferenceCall", "InAMeeting", "Presenting"
    };

    private readonly IPublicClientApplication _msal;
    private readonly HttpClient _http = new();
    private IAccount? _account;

    public GraphService(string clientId)
    {
        _msal = PublicClientApplicationBuilder
            .Create(clientId)
            .WithRedirectUri("http://localhost")
            .WithAuthority(AzureCloudInstance.AzurePublic, "common")
            .Build();
    }

    public async Task<bool> SignInAsync()
    {
        try
        {
            var result = await _msal.AcquireTokenInteractive(Scopes).ExecuteAsync();
            _account = result.Account;
            return true;
        }
        catch { return false; }
    }

    private async Task<string?> GetTokenAsync()
    {
        if (_account is null) return null;
        try
        {
            var result = await _msal.AcquireTokenSilent(Scopes, _account).ExecuteAsync();
            return result.AccessToken;
        }
        catch
        {
            try
            {
                var result = await _msal.AcquireTokenInteractive(Scopes).ExecuteAsync();
                _account = result.Account;
                return result.AccessToken;
            }
            catch { return null; }
        }
    }

    public async Task<bool?> IsInMeetingAsync()
    {
        var token = await GetTokenAsync();
        if (token is null) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/presence");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<PresenceResponse>();
            return json?.Activity is not null && InMeetingActivities.Contains(json.Activity);
        }
        catch { return null; }
    }

    private record PresenceResponse(string? Availability, string? Activity);
}
```

**Step 2: Build and verify**

```bash
dotnet build src/TeamsBusyLight
```

**Step 3: Commit**

```bash
git add src/TeamsBusyLight/GraphService.cs
git commit -m "feat: GraphService — Teams presence polling via Microsoft Graph"
```

---

### Task 4: Settings — Config Persistence

**Files:**
- Create: `src/TeamsBusyLight/Settings.cs`

**Step 1: Create Settings.cs**

```csharp
using System.Text.Json;

namespace TeamsBusyLight;

public class AppSettings
{
    public string ClientId { get; set; } = "";
    public string ComPort { get; set; } = "";
    public int PollIntervalSeconds { get; set; } = 5;

    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
```

**Step 2: Commit**

```bash
git add src/TeamsBusyLight/Settings.cs
git commit -m "feat: AppSettings with JSON persistence"
```

---

### Task 5: TrayApp — System Tray UI + Main Loop

**Files:**
- Create: `src/TeamsBusyLight/TrayApp.cs`
- Modify: `src/TeamsBusyLight/Program.cs`

**Step 1: Create TrayApp.cs**

System tray application with:
- NotifyIcon with green (idle) / red (in meeting) / gray (disconnected) icons (drawn programmatically with Graphics)
- Right-click context menu: "Force ON", "Force OFF", "Auto (Teams)", separator, "Settings...", "Exit"
- Settings dialog: TextBox for Client ID, ComboBox for COM port (populated from SerialService.GetAvailablePorts()), Save button
- Main polling loop using System.Windows.Forms.Timer (5s interval):
  - Call GraphService.IsInMeetingAsync()
  - Compare to previous state
  - If changed, call SerialService.SetLight(bool)
  - Update tray icon color
- Manual override: Force ON/OFF disables polling and sets light directly
- On exit: turn light off, dispose serial

**Step 2: Update Program.cs**

```csharp
namespace TeamsBusyLight;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
```

**Step 3: Build and test**

```bash
dotnet build src/TeamsBusyLight
dotnet run --project src/TeamsBusyLight
```

Expected: Tray icon appears. Settings dialog opens for first-time setup.

**Step 4: Commit**

```bash
git add src/TeamsBusyLight/TrayApp.cs src/TeamsBusyLight/Program.cs
git commit -m "feat: system tray app with presence polling and serial control"
```

---

### Task 6: Build + Final Commit

**Step 1: Build release**

```bash
dotnet publish src/TeamsBusyLight -c Release -r win-x64 --self-contained false
```

**Step 2: Final commit and push**

```bash
git add -A
git commit -m "feat: complete Teams Busy Light implementation"
git push
```
