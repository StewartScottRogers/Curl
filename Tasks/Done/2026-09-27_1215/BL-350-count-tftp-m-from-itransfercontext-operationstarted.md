---
id: BL-350
title: Count TFTP -m from ITransferContext.OperationStarted
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-299]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions/ADR-0040-http-enforces-max-time-and-connect-timeout-in-the-handler.md, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-350 — Count TFTP -m from ITransferContext.OperationStarted

## Goal

`curl -L -m 2` whose chain ends on a `tftp://` hop ends with exit 28 once 2 seconds have passed since the first request, not 2 seconds after the TFTP hop began.

## Context

- Found in BL-299. `ITransferContext.OperationStarted` (ADR-0040, amendment 2026-09-27) carries the timestamp the whole operation began; `RedirectFollower` sets it on every hop after the first, and the HTTP handler counts `-m` from it.
- `TftpTimeLimits` (Curl.Protocol.Tftp.UnitLibrary/TftpTimeLimits.cs) honours `MaxTime` but counts it from its own call, so a redirect to `tftp://` (allowed with `--proto-redir`) restarts `-m`.
- Approach: count `-m` from `OperationStarted ?? now`, as `HttpTransferDeadline` does. Measure curl 8.21.0 (`/mingw64/bin/curl`) for the timeout message's N before pinning it.

## Acceptance criteria

- [x] A `Curl.Protocol.Tftp.UnitTests` test: `MaxTime` 2 s with `OperationStarted` 1.5 s in the past and a server that never answers ends with exit 28 after 0.5 s, with the measured message.
- [x] The `ITransferContext.OperationStarted` remarks and ADR-0040 no longer say TFTP counts `-m` from its own call.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for Curl.Protocol.Tftp.UnitLibrary.

## Notes

- Measured curl 8.21.0 (Git for Windows mingw64) with the extended
  `Record-CurlExchange.ps1 -Port 18351 -ResponseDelayMilliseconds 1500 -Response 'HTTP/1.1 302 Found\r\nLocation: tftp://127.0.0.1:18350/f\r\nContent-Length: 0\r\n\r\n' -CurlArgs '-sS','-L','-m','2','--proto-redir','=tftp','http://127.0.0.1:18351/a'`
  with a silent UDP socket bound on 18350: exit 28, stderr
  `curl: (28) Operation timed out after 2011 milliseconds with 0 bytes received`, wall
  clock about 2.4 s including PowerShell start-up. `-m` and N both count from the first
  request, as for HTTP (BL-299).
- Added `Record-CurlExchange.ps1` to `touches`: it had no way to make a hop take a known
  time, so it gained `-ResponseDelayMilliseconds` (CLAUDE.md: extend it rather than write
  a server). No task in Doing names it.
- Choice: `TftpTimeLimits` turns `MaxTime` into a limit counted from the hop's start by
  subtracting the time the operation ran before the hop, so the re-send schedule and the
  connect timeout keep counting from the hop, and the message adds that time back to
  print the operation's elapsed time. The test pins N at 2000 on the fake clock where
  real curl printed 2011 of wall-clock jitter. Covered for download and upload.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A -L -m chain ending on a tftp:// hop now shares one -m and reports the operation's elapsed time, as curl 8.21.0 does
