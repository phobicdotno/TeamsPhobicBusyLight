# Teams Busy Light — Design Document

## Overview

An Arduino Pro Micro receives serial commands from a C#/.NET system tray app that polls Microsoft Graph API for Teams presence status. The Arduino switches a relay module to control a 12V LED panel — ON during meetings/calls, OFF otherwise.

## Components

### 1. Arduino Pro Micro + Relay Module

- Digital pin 9 → relay module input (5V relay module)
- Relay switches 12V DC circuit to LED panel
- Listens on USB serial (9600 baud) for single-byte commands: `1` = ON, `0` = OFF
- Simple sketch: Serial.read() in loop, digitalWrite() to relay pin

### 2. C# System Tray App (.NET 8)

- Runs minimized in system tray with a busy light icon
- Polls Microsoft Graph `/me/presence` endpoint every 5 seconds
- Maps presence `activity` field:
  - **ON**: `InACall`, `InAConferenceCall`, `InAMeeting`, `Presenting`
  - **OFF**: Everything else (`Available`, `Away`, `Busy` without call, `DoNotDisturb` without call, `Offline`)
- Sends `1` or `0` over serial (COM port) to Arduino
- Tray icon shows current status (green = idle, red = in meeting)
- Right-click menu: manual override ON/OFF, settings, exit

### 3. Microsoft Graph Authentication

- Azure AD app registration with `Presence.Read` permission
- Device code flow or interactive MSAL login on first run
- Token cached locally via MSAL token cache, auto-refresh

## Hardware Wiring

```
Arduino Pro Micro pin 9 → Relay Module IN
Arduino 5V             → Relay VCC
Arduino GND            → Relay GND
Relay NO + COM         → 12V LED panel circuit (breaks the positive wire)
12V PSU                → LED panel (through relay)
```

## Data Flow

```
Microsoft Graph API
    ↓ poll /me/presence every 5s
C# System Tray App
    ↓ compare to last known state
    ↓ if changed: send "1" or "0" over serial
Arduino Pro Micro (USB serial)
    ↓ digitalWrite(RELAY_PIN, HIGH/LOW)
Relay Module
    ↓ switches 12V circuit
LED Panel ON / OFF
```

## Presence Mapping

| Graph Activity         | Light |
|------------------------|-------|
| InACall                | ON    |
| InAConferenceCall      | ON    |
| InAMeeting             | ON    |
| Presenting             | ON    |
| Available              | OFF   |
| Away                   | OFF   |
| BeRightBack            | OFF   |
| Busy                   | OFF   |
| DoNotDisturb           | OFF   |
| Offline                | OFF   |
| PresenceUnknown        | OFF   |

## Tech Stack

| Layer          | Technology                  |
|----------------|-----------------------------|
| Microcontroller| Arduino Pro Micro (ATmega32U4) |
| Switching      | 5V relay module             |
| Light          | 12V DC LED panel            |
| PC App         | C# / .NET 8 WinForms (tray)|
| Auth           | MSAL.NET (Microsoft.Identity.Client) |
| API            | Microsoft Graph REST API    |
| Serial         | System.IO.Ports             |
