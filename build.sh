#!/usr/bin/env sh
# Builds, tests and packages the plugin in the official .NET 10 SDK container.
# Result: dist/comic-thumbnails_<version>.zip (DLLs + meta.json); the version comes from build.yaml.
#
# Other Jellyfin version:  JELLYFIN_VERSION=12.4.0 ./build.sh
set -eu

JELLYFIN_VERSION="${JELLYFIN_VERSION:-12.2.0}"

docker run --rm \
  -v "$(pwd)":/src -w /src \
  -e JELLYFIN_VERSION="$JELLYFIN_VERSION" \
  mcr.microsoft.com/dotnet/sdk:10.0 sh scripts/package.sh
