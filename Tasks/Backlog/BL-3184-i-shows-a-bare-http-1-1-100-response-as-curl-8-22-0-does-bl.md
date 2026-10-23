---
id: BL-3184
title: -i shows a bare HTTP/1.1 100 response as curl 8.22.0 does (BL-3171 item 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerBare100Tests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3184 — -i shows a bare HTTP/1.1 100 response as curl 8.22.0 does (BL-3171 item 2)

## Goal

`-i` shows a bare `HTTP/1.1 100 Continue` response, and a 100 followed by the final response, byte for byte as curl 8.22.0 does.

## Context

BL-3171 item 2. The header reader is `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs` (`IsInformational` in `HttpStatusLine.cs`). Measure both cases against a loopback server (PowerShell or C# file-based app) through curl 8.22.0.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerBare100Tests.cs` shows `-i` output for a bare 100 response, and for a 100 followed by a 200, equal to the bytes curl 8.22.0 prints.
- [ ] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

## Log

- 2026-10-22: Created.
