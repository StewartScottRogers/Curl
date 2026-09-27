---
id: BL-350
title: Count TFTP -m from ITransferContext.OperationStarted
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-299]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions/ADR-0040-http-enforces-max-time-and-connect-timeout-in-the-handler.md]
requirement: none
created: 2026-09-27
completed:
---
# BL-350 — Count TFTP -m from ITransferContext.OperationStarted

## Goal

`curl -L -m 2` whose chain ends on a `tftp://` hop ends with exit 28 once 2 seconds have passed since the first request, not 2 seconds after the TFTP hop began.

## Context

- Found in BL-299. `ITransferContext.OperationStarted` (ADR-0040, amendment 2026-09-27) carries the timestamp the whole operation began; `RedirectFollower` sets it on every hop after the first, and the HTTP handler counts `-m` from it.
- `TftpTimeLimits` (Curl.Protocol.Tftp.UnitLibrary/TftpTimeLimits.cs) honours `MaxTime` but counts it from its own call, so a redirect to `tftp://` (allowed with `--proto-redir`) restarts `-m`.
- Approach: count `-m` from `OperationStarted ?? now`, as `HttpTransferDeadline` does. Measure curl 8.21.0 (`/mingw64/bin/curl`) for the timeout message's N before pinning it.

## Acceptance criteria

- [ ] A `Curl.Protocol.Tftp.UnitTests` test: `MaxTime` 2 s with `OperationStarted` 1.5 s in the past and a server that never answers ends with exit 28 after 0.5 s, with the measured message.
- [ ] The `ITransferContext.OperationStarted` remarks and ADR-0040 no longer say TFTP counts `-m` from its own call.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for Curl.Protocol.Tftp.UnitLibrary.

## Notes

## Log

- 2026-09-27: Created.
