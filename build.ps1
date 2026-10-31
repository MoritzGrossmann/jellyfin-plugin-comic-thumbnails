# Builds, tests and packages the plugin in the .NET 10 SDK container (Docker Desktop).
# Result: dist\comic-thumbnails_<version>.zip (DLLs + meta.json); the version comes from build.yaml.
#
# Usage:                  .\build.ps1
# Other Jellyfin version: .\build.ps1 -JellyfinVersion 12.4.0
param(
    [string]$JellyfinVersion = "12.2.0"
)

$ErrorActionPreference = "Stop"

docker run --rm `
    -v "${PWD}:/src" -w /src `
    -e "JELLYFIN_VERSION=$JellyfinVersion" `
    mcr.microsoft.com/dotnet/sdk:10.0 sh scripts/package.sh

if ($LASTEXITCODE -ne 0) {
    throw "Build failed (exit code $LASTEXITCODE)"
}
