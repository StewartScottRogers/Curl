---
id: BL-5663
title: Match curl's exit code when a server refuses a --max-filesize upload mid-transfer (70 or 55)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpProtocolHandlerUploadTests.cs, Curl.Original.Protocol.Ftp.UnitTests/Fakes/ScriptedConnection.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-21
---
# BL-5663 — Match curl's exit code when a server refuses a --max-filesize upload mid-transfer (70 or 55)

## Goal

When the server refuses an upload mid-transfer (for example because it is over its size limit), the port's exit code is the one curl 8.22.0 gives, every time, not 70 on some runs and 55 on others.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 6 (check). Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

The exit races between 70 (`RemoteDiskFull`) and 55 (`SendError`) when the server refuses mid-upload (Curl.Api reports `RemoteDiskFull` or `SendError` the same way). The race is likely between reading the server's error reply and the send failing on the closed connection. Measure curl 8.22.0 against the same Surl case, 20 runs, and see whether curl is deterministic; then make the port read the server's reply before reporting the send failure (or whatever curl does), and pin it with a test that forces both orderings.

## Acceptance criteria

- [x] The Notes record curl 8.22.0's exit codes over 20 runs of the case.
- [x] The port gives curl's exit code in a test that forces each ordering of the reply and the send failure.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
- touches named EngineToolTransferOutputs.cs, which has nothing to do with the exit code; the race is FTP's (552 vs a failed data write), so touches now names FtpSession.cs and its tests (no other Doing task holds them).
- Measured with a loopback FTP server that sends 552 and closes the data connection after 64 KB (PowerShell, 20 runs each, curl 8.22.0 Schannel): 200 KB upload 70 x19 and 55 x1; 1 MB and 50 MB uploads 55 x20. Curl itself is racy for small uploads, 70 most of the time.
- Port before: 200 KB 55 x19, 70 x1; 1 MB and 50 MB 55 x20. After: 200 KB 70 x20, large ones unchanged 55.
- Decision (ADR-1326): on a failed data write wait up to 500 ms for the control reply; 552 gives 70, else 55.
- Tests force both orders: reply already read, reply arriving after the write failed (async read), and no reply (exit 55). Quality run for Curl.Original.Protocol.Ftp.UnitLibrary: 100% line and branch, 0 failing. Linux/macOS: tests are platform neutral.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 6 (check).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 2efbce0f 359c8009 3d836d5e.
- 2026-11-21: Doing -> Done. Failed FTP upload write waits for 552 reply; exit 70 deterministic (ADR-1326)
