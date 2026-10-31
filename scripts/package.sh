#!/usr/bin/env sh
# Tests and packages the plugin. Runs inside the .NET SDK container (see build.sh / build.ps1) or on a CI runner.
# The plugin version and changelog come from build.yaml.
set -eu
: "${JELLYFIN_VERSION:=12.2.0}"

PLUGIN_VERSION=$(sed -n 's/^version: *"\([0-9.]*\)".*/\1/p' build.yaml | head -n1)
# JSON-safe single line: backslashes dropped, double quotes become single quotes
CHANGELOG=$(sed -n '/^changelog:/,$p' build.yaml | sed '1d' | sed 's/^ *//' | paste -sd ' ' - | tr -d '\134' | tr '"' "'")
[ -n "$PLUGIN_VERSION" ] || { echo "No version found in build.yaml" >&2; exit 1; }

dotnet test Jellyfin.Plugin.ComicThumbnails.Tests -p:JellyfinVersion=$JELLYFIN_VERSION
dotnet publish Jellyfin.Plugin.ComicThumbnails -c Release -o out -p:JellyfinVersion=$JELLYFIN_VERSION
rm -rf dist/pkg && mkdir -p dist/pkg
cp out/Jellyfin.Plugin.ComicThumbnails.dll out/SharpCompress.dll dist/pkg/
cat > dist/pkg/meta.json <<JSON
{
  "category": "General",
  "changelog": "${CHANGELOG}",
  "description": "Shows thumbnails of the pages of a comic on its detail page. Thumbnails are loaded page by page and cached; clicking one opens the comic reader at that page.",
  "imageUrl": "https://raw.githubusercontent.com/MoritzGrossmann/jellyfin-plugin-comic-thumbnails/master/images/comic-thumbnails.png",
  "guid": "8d5e547a-736f-4544-b9f7-615fb5b453da",
  "name": "Comic Thumbnails",
  "overview": "Page thumbnails on the comic detail page, click one to open the reader at that page",
  "owner": "moritz",
  "targetAbi": "${JELLYFIN_VERSION}.0",
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "version": "${PLUGIN_VERSION}",
  "status": "Active",
  "autoUpdate": false,
  "assemblies": ["Jellyfin.Plugin.ComicThumbnails.dll", "SharpCompress.dll"]
}
JSON
if ! command -v zip >/dev/null 2>&1; then
  apt-get update -qq >/dev/null && apt-get install -y -qq zip >/dev/null
fi
rm -f dist/comic-thumbnails_${PLUGIN_VERSION}.zip
cd dist/pkg && zip -q ../comic-thumbnails_${PLUGIN_VERSION}.zip * && cd ../..
rm -rf dist/pkg out
chown -R $(stat -c %u:%g /src) dist 2>/dev/null || true
echo "Done: dist/comic-thumbnails_${PLUGIN_VERSION}.zip"
