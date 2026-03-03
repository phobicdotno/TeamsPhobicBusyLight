# Teams Busy Light

<p align="center">
  <strong>Automatic "On Air" light that turns on when you're in a Teams meeting or call.</strong>
</p>

## How It Works

A **C# system tray app** polls your Microsoft Teams presence status via the Graph API every 5 seconds. When you join a meeting or call, it sends a serial command to an **Arduino Pro Micro**, which switches a **relay module** to turn on a **12V LED panel**.

```
Teams Meeting → Graph API → C# Tray App → USB Serial → Arduino → Relay → 12V LED Panel
```

## Hardware

| Part | Description |
|------|-------------|
| Arduino Pro Micro | ATmega32U4, native USB |
| 5V Relay Module | Single-channel, opto-isolated |
| 12V LED Panel | Any 12V DC light source |
| 12V Power Supply | Matched to your LED panel |
| USB Cable | Micro-USB to PC |

### Wiring

```
Arduino Pin 9  → Relay IN
Arduino 5V     → Relay VCC
Arduino GND    → Relay GND
Relay NO + COM → 12V LED circuit (breaks positive wire)
12V PSU        → LED panel (through relay)
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building from source)
- [Arduino IDE](https://www.arduino.cc/en/software) (for uploading the sketch)
- A Microsoft work/school account with Teams
- An Azure AD app registration (see Setup below)

## Setup

### 1. Azure AD App Registration

1. Go to [Azure Portal → App Registrations](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)
2. Click **New registration**
3. Name it `Teams Busy Light`
4. Under **Supported account types**, select "Accounts in any organizational directory"
5. Set **Redirect URI** to `http://localhost` (platform: "Mobile and desktop applications")
6. Click **Register**
7. Under **API Permissions**, click **Add a permission** → **Microsoft Graph** → **Delegated** → search `Presence.Read` → **Add**
8. Copy the **Application (client) ID** — you'll need it later

### 2. Arduino

1. Open `arduino/BusyLight/BusyLight.ino` in Arduino IDE
2. Select board: **Arduino Leonardo** (Pro Micro uses the same chip)
3. Select the correct COM port
4. Click **Upload**
5. Note the COM port number (e.g. `COM3`)

### 3. Install the PC App

#### Option A: MSI Installer (recommended)

1. Download or build the MSI (see [Building](#building) below)
2. Run `TeamsBusyLight.msi`
3. The app installs to `C:\Program Files\TeamsBusyLight\`
4. A Start Menu shortcut and a Startup entry are created (auto-launches on boot)

#### Option B: Run from source

```bash
dotnet run --project src/TeamsBusyLight
```

### 4. First Launch

1. The app appears as a **gray circle** in the system tray
2. A settings dialog opens — enter your **Azure Client ID** and select the **COM port**
3. Click **Save & Connect**
4. A browser window opens for Microsoft sign-in — authorize the app
5. The tray icon turns **green** (available) or **red** (in meeting)
6. The light turns on automatically when you join a Teams meeting or call

### 5. Tray Menu (right-click the icon)

| Option | Description |
|--------|-------------|
| **Force ON** | Turns the light on, ignores Teams status |
| **Force OFF** | Turns the light off, ignores Teams status |
| **Auto (Teams)** | Resumes automatic Teams-based control |
| **Settings...** | Change Client ID or COM port |
| **Exit** | Turns light off and closes the app |

## Building

### Build the C# app

```bash
dotnet build src/TeamsBusyLight -c Release
```

### Publish as self-contained exe

```bash
dotnet publish src/TeamsBusyLight -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Output: `publish/TeamsBusyLight.exe` (~54 MB standalone, no .NET runtime required)

### Build the MSI installer

Requires [WiX Toolset](https://wixtoolset.org/) (`dotnet tool install --global wix`):

```bash
cd installer
wix build TeamsBusyLight.wxs -arch x64 -o TeamsBusyLight.msi
```

The MSI installs the app to `C:\Program Files\TeamsBusyLight\` with Start Menu and Startup shortcuts.

## Project Structure

```
TeamsBusyLight/
├── arduino/
│   └── BusyLight/
│       └── BusyLight.ino           # Arduino sketch (serial → relay)
├── installer/
│   └── TeamsBusyLight.wxs          # WiX MSI installer source
├── src/
│   └── TeamsBusyLight/             # C# .NET 8 WinForms tray app
│       ├── Program.cs              # Entry point
│       ├── TrayApp.cs              # System tray UI + polling loop
│       ├── GraphService.cs         # Microsoft Graph presence API
│       ├── SerialService.cs        # Arduino USB serial communication
│       └── Settings.cs             # JSON config persistence
├── docs/
│   └── plans/                      # Design docs and implementation plans
├── CHANGELOG.md
└── README.md
```

## Teams Presence Mapping

| Teams Activity       | Light |
|----------------------|-------|
| InACall              | ON    |
| InAConferenceCall    | ON    |
| InAMeeting           | ON    |
| Presenting           | ON    |
| Available            | OFF   |
| Away                 | OFF   |
| BeRightBack          | OFF   |
| Busy                 | OFF   |
| DoNotDisturb         | OFF   |
| Offline              | OFF   |

## License

MIT
