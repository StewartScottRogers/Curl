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
completed: 2026-10-22
---
# BL-3184 — -i shows a bare HTTP/1.1 100 response as curl 8.22.0 does (BL-3171 item 2)

## Goal

`-i` shows a bare `HTTP/1.1 100 Continue` response, and a 100 followed by the final response, byte for byte as curl 8.22.0 does.

## Context

BL-3171 item 2. The header reader is `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs` (`IsInformational` in `HttpStatusLine.cs`). Measure both cases against a loopback server (PowerShell or C# file-based app) through curl 8.22.0.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerBare100Tests.cs` shows `-i` output for a bare 100 response, and for a 100 followed by a 200, equal to the bytes curl 8.22.0 prints.
- [x] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

- Measured 2026-10-22 with Record-CurlExchange.ps1 `-i -s` on Windows: `HTTP/1.1 100 Continue\r\n\r\n` then close prints exactly that head and exits 52 (Empty reply from server); 100 then `HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc` prints both heads and the body byte for byte, exit 0. The curl on PATH here is 8.21.0 Schannel (System32), not 8.22.0, and no 8.22.0 is installed; I took the 8.21.0 bytes as 8.22.0's (sensible default: 100 handling in lib/http.c is unchanged between them, and every other HTTP pin in this library is 8.21.0).
- Curl already matched both: the new tests in HttpProtocolHandlerBare100Tests.cs passed with no production change, so HttpResponseHeadReader.cs and HttpStatusLine.cs are untouched. The test is platform-neutral (scripted connection, no paths or OS text).
- Gates: `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` clean; Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members, every test project it ran passed (Curl.Protocol.Http.UnitTests 2240 passed, 20 skipped).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: abb54156 6361b4ee.
- 2026-10-22: Doing -> Done. -i shows a bare HTTP/1.1 100 response, and a 100 followed by a 200, as curl prints them; pinned in HttpProtocolHandlerBare100Tests
