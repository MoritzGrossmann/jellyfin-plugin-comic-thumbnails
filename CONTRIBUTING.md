# Contributing

Thanks for your interest in Comic Thumbnails! Bug reports, feature ideas and pull requests are welcome.

## Reporting bugs and requesting features

Open an [issue](https://github.com/MoritzGrossmann/jellyfin-plugin-comic-thumbnails/issues) and include:

- Jellyfin server version and plugin version (*Dashboard → Plugins*),
- how you run Jellyfin (Windows, Linux package, Docker),
- the comic format (`.cbz`, `.cbr`, `.cb7`, `.cbt`) and, if possible, a small archive that reproduces the problem,
- relevant lines from the Jellyfin log.

Please check existing issues first to avoid duplicates.

## Development

See [Building](README.md#building) for how to build and test. In short:

```sh
dotnet test
dotnet publish Jellyfin.Plugin.ComicThumbnails -c Release -o out
```

Or use `build.ps1` / `build.sh` (Docker only, no local .NET needed).

## Pull requests

- Keep each pull request focused on one change.
- Add or update unit tests for behavior changes (`Jellyfin.Plugin.ComicThumbnails.Tests`).
- Make sure `dotnet test` passes; the CI workflow runs it on every push.
- Follow the style in `.editorconfig`.
- Update the README if you change user-visible behavior.
- Write commit messages as [Conventional Commits](https://www.conventionalcommits.org), e.g. `feat: add page size setting`, `fix: handle empty archives`, `docs: clarify install steps`.
- Write all code, comments, UI text and documentation in English.

Releases are created by the maintainer, see [Versioning and releases](README.md#versioning-and-releases). Do not change `version` in `build.yaml` or the manifests in a pull request.

## Code of Conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to uphold it.
