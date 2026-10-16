---
id: BL-2465
title: Engine: ManagedCurlEngine.VersionInfo reports curl -V's host, TLS backend, protocols and features
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2458]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl.UnitTests/Misc/Version, Libcurl.UnitTests/Libcurl.UnitTests.csproj]
requirement: none
created: 2026-10-16
completed:
---
# BL-2465 — Engine: ManagedCurlEngine.VersionInfo reports curl -V's host, TLS backend, protocols and features

## Goal

`ManagedCurlEngine.VersionInfo` describes the same build `curl -V` (`Curl.Cli`'s `CurlVersionText`) does on the running platform, so Libcurl's `CurlVersion.Text` and `CurlVersion.Info` (BL-2458) agree with `curl -V` over the real engine.

## Context

Split from BL-2458, whose Libcurl `CurlVersion` reads everything from `ICurlEngine.VersionInfo` (ADR-0509). Today the managed engine fills only `Protocols`, and lists `smb`/`smbs`, which neither reference build lists (ADR-0453, BL-2334); `Features` is empty, `Host` empty and `TlsBackend` null. `Curl.Engine.UnitLibrary` was held by BL-2424 when BL-2458 ran, so the engine side is this task.

What `curl -V` implies for libcurl's `curl_version_info` (curl 8.22.0):
- `Host`: the triple in `-V`'s first line (`x86_64-w64-mingw32` on Windows, `aarch64-apple-darwin25.0.0` on macOS, `x86_64-pc-linux-gnu` elsewhere).
- `TlsBackend`: `Schannel`, `SecureTransport` or `OpenSSL`, the word after `libcurl/8.22.0` in that line, so `CurlVersion.Text` is `libcurl/8.22.0 <backend>`, the line's tail.
- `Protocols`: `-V`'s `Protocols:` names without `ipfs` and `ipns`, which the curl tool adds itself (`src/tool_libinfo.c`), not libcurl.
- `Features`: `-V`'s `Features:` names (Windows's line on Windows).

Curl.Engine.UnitLibrary does not reference `Curl.Cli.UnitLibrary`; either pin the values in the engine with a test in `Curl.Engine.UnitTests` (which references `Curl.Cli.UnitLibrary`) checking them against `CurlVersionText.Lines`, or decide another single source and record it in an ADR.

## Acceptance criteria

- [ ] A test in `Curl.Engine.UnitTests` checks `ManagedCurlEngine.VersionInfo`'s host, TLS backend, protocols (plus `ipfs`, `ipns`) and features against `CurlVersionText.Lines` for the running platform.
- [ ] A test in `Libcurl.UnitTests/Misc/Version` checks `CurlVersion.Text` and `CurlVersion.Info(CurlVersionAge.Now)` over a real `ManagedCurlEngine` against `curl -V`'s lines (`CurlVersionText`), adding a `Curl.Cli.UnitLibrary` reference to `Libcurl.UnitTests` if needed.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
