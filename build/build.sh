#!/bin/bash

set -euo pipefail

# Configuration
PLUGIN_NAME="Jellyfin.Plugin.Jellysleep"
SOLUTION_FILE="../${PLUGIN_NAME}.sln"
BUILD_DIR="../bin"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Parse command line arguments
CONFIGURATION="Release"
CLEAN=false
PACKAGE=false
VERSION=""

while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        -p|--package)
            PACKAGE=true
            shift
            ;;
        -v|--version)
            VERSION="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -c, --configuration CONF  Build configuration (Debug|Release) [default: Release]"
            echo "  --clean                   Clean before build"
            echo "  -p, --package             Create plugin package"
            echo "  -v, --version VERSION     Set version before build"
            echo "  -h, --help                Show this help"
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

log_info "Building Jellyfin Plugin: $PLUGIN_NAME"
log_info "Configuration: $CONFIGURATION"

# Update version if provided
if [ -n "$VERSION" ]; then
    log_info "Updating version to: $VERSION"
    ./update-version.sh "$VERSION"
fi

# Clean if requested
if [ "$CLEAN" = true ]; then
    log_info "Cleaning previous builds..."
    dotnet clean "$SOLUTION_FILE" --configuration "$CONFIGURATION" --verbosity minimal
    rm -rf "$BUILD_DIR"
fi

# Restore packages
log_info "Restoring NuGet packages..."
dotnet restore "$SOLUTION_FILE" --verbosity minimal

# Build
log_info "Building solution..."
dotnet build "$SOLUTION_FILE" \
    --configuration "$CONFIGURATION" \
    --no-restore \
    --verbosity minimal \
    /property:GenerateFullPaths=true \
    /consoleloggerparameters:NoSummary

# Publish
log_info "Publishing plugin..."
dotnet publish "$SOLUTION_FILE" \
    --configuration "$CONFIGURATION" \
    --no-build \
    --verbosity minimal \
    /property:GenerateFullPaths=true \
    /consoleloggerparameters:NoSummary

# Create package if requested
if [ "$PACKAGE" = true ]; then
    log_info "Creating plugin package..."

    # Get version from project file
    if [ -z "$VERSION" ]; then
        VERSION=$(grep -o '<PluginVersion>[^<]*</PluginVersion>' "../${PLUGIN_NAME}/${PLUGIN_NAME}.csproj" | sed 's/<[^>]*>//g')
    fi

    PACKAGE_NAME="jellyfin-plugin-jellysleep-${VERSION}.zip"
    PACKAGE_PATH="${BUILD_DIR}/${PACKAGE_NAME}"

    # The MSBuild target handles all the file filtering and packaging
    log_info "Package will be created by MSBuild target..."

    # Check if package was created successfully
    if [ -f "$PACKAGE_PATH" ]; then
        log_info "Package created: $PACKAGE_PATH"

        # Calculate package size and hash
        PACKAGE_SIZE=$(du -h "$PACKAGE_PATH" | cut -f1)
        PACKAGE_HASH=$(sha256sum "$PACKAGE_PATH" | cut -d' ' -f1)

        log_info "Package size: $PACKAGE_SIZE"
        log_info "SHA256: $PACKAGE_HASH"

        # Show package contents
        log_info "Package contents:"
        if command -v unzip >/dev/null 2>&1; then
            unzip -l "$PACKAGE_PATH" | grep '\.dll$' | awk '{print "  - " $4}'
        else
            log_info "  (install unzip to see package contents)"
        fi
    else
        log_warn "Package not found at expected location: $PACKAGE_PATH"
        log_info "Check if zip command is available or package was created in plugin directory"
    fi
fi

log_info "Build completed successfully!"
