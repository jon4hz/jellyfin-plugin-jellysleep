#!/bin/bash

set -euo pipefail

# Check if version argument is provided
if [ $# -eq 0 ]; then
    echo "Error: Version argument is required"
    echo "Usage: $0 <version>"
    echo "Example: $0 1.0.0"
    exit 1
fi

VERSION="$1"

# Validate version format (semver)
if ! [[ "$VERSION" =~ ^v?[0-9]+\.[0-9]+\.[0-9]+(-.*)?$ ]]; then
    echo "Error: Invalid version format. Expected semver format (e.g., 1.0.0 or 1.0.0-beta.1)"
    exit 1
fi

# Remove 'v' prefix if present
CLEAN_VERSION="${VERSION#v}"

# For assembly version, use only major.minor.patch (no pre-release)
ASSEMBLY_VERSION="${CLEAN_VERSION%%-*}.0"

echo "Updating version to: $CLEAN_VERSION"
echo "Assembly version: $ASSEMBLY_VERSION"

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/../Jellyfin.Plugin.Jellysleep/Jellyfin.Plugin.Jellysleep.csproj"

if [ ! -f "$PROJECT_FILE" ]; then
    echo "Error: Project file not found: $PROJECT_FILE"
    exit 1
fi

# Update version properties in project file
sed -i "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$ASSEMBLY_VERSION</AssemblyVersion>|g" "$PROJECT_FILE"
sed -i "s|<FileVersion>.*</FileVersion>|<FileVersion>$ASSEMBLY_VERSION</FileVersion>|g" "$PROJECT_FILE"
sed -i "s|<PluginVersion>.*</PluginVersion>|<PluginVersion>$CLEAN_VERSION</PluginVersion>|g" "$PROJECT_FILE"

# Update version in plugin script injection
PLUGIN_FILE="$SCRIPT_DIR/../Jellyfin.Plugin.Jellysleep/JellysleepPlugin.cs"

if [ -f "$PLUGIN_FILE" ]; then
    sed -i "s|version=\"[0-9.]*\"|version=\"$CLEAN_VERSION\"|g" "$PLUGIN_FILE"
fi

echo "Version updated successfully!"
