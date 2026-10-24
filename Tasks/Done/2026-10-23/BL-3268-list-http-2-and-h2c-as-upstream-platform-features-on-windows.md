---
id: BL-3268
title: List http/2 and h2c as upstream platform features on Windows now that --http2 runs there
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3207]
touches: [Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs, Curl.Conformance.UnitLibrary/UpstreamUnixFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitLibrary/UpstreamHttp2Features.cs, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3268 — List http/2 and h2c as upstream platform features on Windows now that --http2 runs there

## Goal

The upstream case runner on Windows reports the `http/2` and `h2c` features, so upstream cases that need HTTP/2 run on Windows as they do on Linux and macOS.

## Context

BL-3207 (ADR-0786) turned `--http2`, `--http2-prior-knowledge` and `--proxy-http2` on for Windows and added `HTTP2` to the Windows `curl -V` Features line. `UpstreamCurlPlatform` still lists `http/2` and `h2c` off Windows only (BL-2230; they sit in `UpstreamUnixFeatures.cs`). Turning them on changes which upstream cases run on Windows, so run the HTTP/2 cases on Windows, hold the passing ones on the ratchet and leave failures to tasks; check each case is platform-neutral first.

## Acceptance criteria

- [x] `UpstreamCurlPlatform.Windows.Features` contains `http/2` and `h2c`, and a test pins it.
- [x] The remarks in `UpstreamCurlPlatform.cs` no longer say BL-3264 is pending; they name this change.
- [x] Build clean, fast tests pass, and the conformance ratchet holds on Windows.

## Notes

- Moved http/2 and h2c to every platform (new UpstreamHttp2Features.cs in Common). Added Curl.Conformance.UnitTests and the new file to touches.
- test1704 exits 0 on Windows where 8 is expected; it is not held on Windows (Http2WindowsCases.cs) until BL-3270 fixes it. Platform-neutral; CI on Linux and macOS unaffected.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 67d42b28 bb146c16 afd7f1cd.
- 2026-10-23: Doing -> Done. Windows lists http/2 and h2c; ratchet holds, test1704 left to BL-3270
