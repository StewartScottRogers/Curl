---
id: BL-2517
title: Pack Curl.Mcp as native AOT per OS in ci.yml's mcp-pack job and turn PublishAot on in Curl.Mcp.csproj
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/ci.yml, Curl.Mcp/Curl.Mcp.csproj, Curl.Mcp/README.md, Documentation/Planning/Decisions/ADR-0621-curl-mcp-runs-native-aot-once-its-json-options-are-built-on-first-use.md]
lane: no
requirement: none
created: 2026-10-16
completed:
---
# BL-2517 — Pack Curl.Mcp as native AOT per OS in ci.yml's mcp-pack job and turn PublishAot on in Curl.Mcp.csproj

## Goal

`ci.yml`'s `mcp-pack` job packs Curl.Mcp the way `release.yml`'s `pack-mcp` does, native AOT per runtime on its own OS, and `Curl.Mcp.csproj` sets `<PublishAot>true</PublishAot>`, so every Curl.Mcp package CI or a release builds is native.

## Context

- Left by BL-2486 (ADR-0621 decision 2) for an interactive session: `ci.yml` is a guard file, so a lane may not change it. BL-2486 made Curl.Mcp run as native AOT (the cause was `CurlMcpJsonContext.ToolOptions` being built before the generator set `Default`; now `field ??= ...`) and gave `release.yml` a 7-leg `pack-mcp` matrix, passing `-p:PublishAot=true` on the command line.
- Today `mcp-pack` runs `dotnet pack Curl.Mcp -c Release` on windows, ubuntu and macos and expects 7 self-contained packages from each. With `PublishAot` on, a plain pack gives only the tool package, and each runtime package needs `dotnet pack -r <rid> -p:PublishAot=true` on its own OS. Mirror `release.yml`'s `pack-mcp` checks: the tool package carries `.mcp/server.json`; each runtime package holds its native binary and no `Curl.Mcp.dll`, and answers `initialize`.
- Keep the job's `Curl.Mcp Integration tests` step. Runtimes per runner: windows-latest win-x64, ubuntu-latest linux-x64, macos-latest osx-arm64 (as release.yml; arm64 Windows and Linux and Intel macOS need their own runners there).
- Change on the `audit` branch; its pull request merges under the standing exception once CI passes on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `ci.yml`'s `mcp-pack` packs the tool package once and one native runtime package per runner's runtime, and checks each as `release.yml`'s `pack-mcp` does, including an `initialize` answer from the extracted native binary.
- [ ] `Curl.Mcp.csproj` sets `<PublishAot>true</PublishAot>` with its comment saying so; `release.yml` may keep passing the property.
- [ ] ADR-0621 decision 2 and `Curl.Mcp/README.md`'s Package section say what the build now does.
- [ ] Locally, `dotnet pack Curl.Mcp -c Release -r win-x64` with vswhere on PATH gives a package holding a native `Curl.Mcp.exe` and no `Curl.Mcp.dll`; `dotnet build` is clean and the fast tests pass.
- [ ] The pull request's CI passed on Windows, Linux and macOS, with the new `mcp-pack` legs green; it is linked under Notes.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 0e69d2b2 393380d2 dd713eb4 100b6ff2 c692587d.
