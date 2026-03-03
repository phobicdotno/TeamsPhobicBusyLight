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

## Software

### Arduino Sketch

Listens on serial for `1` (light ON) or `0` (light OFF) and drives the relay pin.

### C# System Tray App (.NET 8)

- Polls Microsoft Graph `/me/presence` every 5 seconds
- Light turns **ON** for: `InACall`, `InAConferenceCall`, `InAMeeting`, `Presenting`
- Light turns **OFF** for everything else
- System tray icon shows current status (green/red)
- Right-click menu: manual override, settings, exit

## Setup

### 1. Azure AD App Registration

1. Go to [Azure Portal → App Registrations](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)
2. New registration → name it "Teams Busy Light"
3. Set redirect URI to `http://localhost` (Mobile and desktop applications)
4. Under API Permissions, add **Microsoft Graph → Presence.Read** (delegated)
5. Copy the **Application (client) ID**

### 2. Arduino

1. Open `arduino/BusyLight/BusyLight.ino` in Arduino IDE
2. Upload to your Pro Micro
3. Note the COM port

### 3. PC App

1. Build and run the C# tray app
2. On first launch, sign in with your Microsoft account
3. Select the correct COM port in settings
4. Minimize — it runs in the system tray

## Project Structure

```
TeamsBusyLight/
├── arduino/
│   └── BusyLight/
│       └── BusyLight.ino        # Arduino sketch
├── src/
│   └── TeamsBusyLight/          # C# .NET 8 project
│       ├── Program.cs
│       ├── TrayApp.cs           # System tray UI
│       ├── GraphService.cs      # Teams presence polling
│       ├── SerialService.cs     # Arduino serial communication
│       └── Settings.cs          # COM port, client ID config
├── docs/
│   └── plans/
│       └── 2026-03-03-teams-busy-light-design.md
└── README.md
```

## License

MIT
