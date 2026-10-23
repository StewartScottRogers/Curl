---
id: BL-3264
title: List http/2 and h2c as upstream platform features on Windows now that --http2 runs there
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3207]
touches: [Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs, Curl.Conformance.UnitLibrary/UpstreamUnixFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3264 — List http/2 and h2c as upstream platform features on Windows now that --http2 runs there

## Goal

The upstream case runner on Windows reports the `http/2` and `h2c` features, so upstream cases that need HTTP/2 run on Windows as they do on Linux and macOS.

## Context

BL-3207 (ADR-0786) turned `--http2`, `--http2-prior-knowledge` and `--proxy-http2` on for Windows and added `HTTP2` to the Windows `curl -V` Features line. `UpstreamCurlPlatform` still lists `http/2` and `h2c` off Windows only (BL-2230; they sit in `UpstreamUnixFeatures.cs`). Turning them on changes which upstream cases run on Windows, so run the HTTP/2 cases on Windows, hold the passing ones on the ratchet and leave failures to tasks; check each case is platform-neutral first.

## Acceptance criteria

- [ ] `UpstreamCurlPlatform.Windows.Features` contains `http/2` and `h2c`, and a test pins it.
- [ ] The remarks in `UpstreamCurlPlatform.cs` no longer say BL-3264 is pending; they name this change.
- [ ] Build clean, fast tests pass, and the conformance ratchet holds on Windows.

## Notes

## Log

- 2026-10-23: Created.
