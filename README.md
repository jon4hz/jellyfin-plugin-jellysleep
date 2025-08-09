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
    - [🐳 Docker Installation Notes](#-docker-installation-notes)
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

### 🐳 Docker Installation Notes

If you're running Jellyfin in Docker, the plugin may not have permission to modify the web interface files. If you see permission errors in your logs, you'll need to map the `index.html` file manually:

1. Copy the index.html file from your container:

   ```bash
   docker cp jellyfin:/usr/share/jellyfin/web/index.html /path/to/your/jellyfin/config/index.html
   ```

2. Add a volume mapping to your Docker run command:

   ```bash
   -v /path/to/your/jellyfin/config/index.html:/usr/share/jellyfin/web/index.html
   ```

3. Or for Docker Compose, add this to your volumes section:
   ```yaml
   services:
     jellyfin:
       # ... other config
       volumes:
         - /path/to/your/jellyfin/config:/config
         - /path/to/your/jellyfin/config/index.html:/usr/share/jellyfin/web/index.html
         # ... other volumes
   ```

This gives the plugin the necessary permissions to inject the sleep timer JavaScript into the web interface.

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
