---
id: BL-3960
title: CGR-107f: -w num_headers for a ws 101 (3) and RTSP (2), and http_version 1.1 for RTSP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary/WsProtocolHandler.cs, Curl.Protocol.Ws.UnitTests/Cgr107Tests.cs, Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs, Curl.Protocol.Rtsp.UnitTests/Cgr107Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3960 — CGR-107f: -w num_headers for a ws 101 (3) and RTSP (2), and http_version 1.1 for RTSP

## Goal

`%{num_headers}` counts a ws 101's and an RTSP response's header lines, and `%{http_version}` is 1.1 for RTSP, as curl 8.22.0 does.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. OP-11: `-o NUL -w "%{num_headers}" ws://...` prints 3 with curl (101 response) and 0 with Curl; `-w "%{num_headers} %{http_version}" rtsp://127.0.0.1:PORT/m` prints `2 1.1` with curl and `0 0` with Curl. The handlers leave the report's header count and HTTP version unset.

## Acceptance criteria

- [ ] Both repros print what real curl prints.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [ ] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

## Log

- 2026-10-28: Created.
