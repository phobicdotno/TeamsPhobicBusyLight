# Changelog

## [1.0.0] - 2026-03-03

### Added
- Arduino Pro Micro sketch — listens on USB serial for `1`/`0` commands, drives relay on pin 9
- C# .NET 8 system tray application
  - Polls Microsoft Graph `/me/presence` every 5 seconds
  - Detects Teams meeting/call status (InACall, InAConferenceCall, InAMeeting, Presenting)
  - Sends serial commands to Arduino to switch 12V relay
  - System tray icon with color status (green = available, red = in meeting, gray = disconnected)
  - Right-click context menu: Force ON, Force OFF, Auto (Teams), Settings, Exit
  - Settings dialog for Azure Client ID and COM port selection
  - JSON-based settings persistence
- MSAL authentication with interactive sign-in and silent token refresh
- MSI installer (WiX Toolset)
  - Installs to `C:\Program Files\TeamsBusyLight\`
  - Start Menu shortcut
  - Windows Startup shortcut (auto-launch on boot)
  - Clean uninstall via Apps & Features
