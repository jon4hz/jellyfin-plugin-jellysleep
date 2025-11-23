# 🌙 Jellysleep

Sleep timer plugin for Jellyfin that automatically stops playback after a specified time or episodes.

<img src="assets/jellysleep.png" alt="Jellysleep Plugin Logo" width="20%">

## ✨ Features

- **Timer-based Sleep**: Set sleep timers for 15 minutes, 30 minutes, 1 hour, or 2 hours
- **Episode-based Sleep**: Stop playback after the current episode ends
- **Per-user, Per-device**: Each user session can have its own independent sleep timer
- **Auto-pause**: Automatically pauses playback when the timer expires
- **Web Interface**: Easy-to-use sleep timer controls directly in the Jellyfin web player

## 📋 Table of Contents

- [🌙 Jellysleep](#-jellysleep)
  - [✨ Features](#-features)
  - [📋 Table of Contents](#-table-of-contents)
  - [📱 Supported Devices](#-supported-devices)
  - [📦 Installation](#-installation)
    - [Requirements](#requirements)
    - [Install Plugin](#install-plugin)
  - [🚀 Usage](#-usage)
  - [🛠️ Development](#️-development)
    - [Building](#building)
    - [Packaging](#packaging)
  - [📜 License](#-license)

## 📱 Supported Devices

This plugin works by injecting custom JavaScript into Jellyfin's web interface. It is compatible with:

- ✅ **Jellyfin Web UI** (browser access)
- ✅ **Jellyfin Android App** (uses embedded web UI)
- ✅ **Jellyfin iOS App** (uses embedded web UI)
- ✅ **Jellyfin Desktop Apps** (Flatpak, etc. - uses embedded web UI)
- ⏳️ **Streamyfin** (work in progress)
- ❌ **Android TV App** (uses native interface, cannot be modified)
- ❌ **Other native apps** that don't use the web interface

## 📦 Installation

### Requirements

This plugin requires the following plugins to be installed as well:

- [Jellyfin-JavaScript-Injector](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector)
- [jellyfin-plugin-file-transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) (optional, but recommended)

### Install Plugin

1. Open your Jellyfin server's admin dashboard
2. Navigate to **Plugins** → **Catalog**
3. Click the **Add Repository** button
4. Add this repository URL:
   ```
   https://raw.githubusercontent.com/jon4hz/jellyfin-plugin-jellysleep/main/manifest.json
   ```
5. Find **Jellysleep** in the plugin catalog and install it
6. Restart your Jellyfin server
7. Enable the plugin in **Plugins** → **My Plugins**

## 🚀 Usage

Once installed and enabled, you'll see a sleep timer button (moon icon) in the Jellyfin web player controls. Click it to:

- Set a duration-based timer (15min, 30min, 1h, 2h)
- Set an episode-based timer (stops after current episode)
- Cancel active timers

## 🛠️ Development

### Building

```bash
dotnet build
```

### Packaging

```bash
make package
```

The plugin follows Jellyfin's plugin architecture and integrates with the session management system to track playback state and automatically pause content when timers expire.

## 📜 License

GPLv3
