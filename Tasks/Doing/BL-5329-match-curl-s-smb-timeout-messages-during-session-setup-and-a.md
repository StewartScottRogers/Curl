---
id: BL-5329
title: Match curl's SMB timeout messages during session setup and after the open
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/MaxTimeWatchdog.cs, Curl.Core.UnitTests/MaxTimeWatchdogTests.cs, Curl.Original.Protocol.Smb.UnitLibrary/SmbFileTransfer.cs, Curl.Original.Protocol.Smb.UnitLibrary/SmbSessionEstablisher.cs, Curl.Original.Protocol.Smb.UnitTests/SmbTimeoutMessageTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5329 — Match curl's SMB timeout messages during session setup and after the open

## Goal

An SMB `-m` timeout prints `Operation timed out after N milliseconds with 0 bytes received` while waiting for session setup, and `... with 0 out of M bytes received` (with progress total M and a `* ...` line under `-v`) once the file is open, as curl does; exit 28 is unchanged.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-080 (Low: message only, exit 28 matches; Confirmed against curl 8.18 twice each, Probable for 8.22 which has no SMB). Area: smb://, `-m` timeout messages, `-v`, progress meter; Windows and Linux.

**Repro.** An SMB server on 127.0.0.1:33447 (PowerShell or C# file-based stand-in, never Python) that answers negotiate normally, then (a) never answers SESSION_SETUP_ANDX; (b) answers setup, tree connect and NT_CREATE_ANDX (EndOfFile = 10), then never answers READ_ANDX.
- (a) `curl -sS -m 3 -u alice:secret smb://127.0.0.1:33447/SHARE/hello.txt`
- (b) the same with `-v`

**Expected (curl 8.18).** (a) `curl: (28) Operation timed out after 3000 milliseconds with 0 bytes received`. (b) `* Operation timed out after 3000 milliseconds with 0 out of 10 bytes received`, `* closing connection #0`, then `curl: (28) Operation timed out after 3000 milliseconds with 0 out of 10 bytes received`; progress total 10.

**Actual (port).** (a) `curl: (28) Connection timed out after 3010 milliseconds`. (b) only `curl: (28) Operation timed out after 3015 milliseconds with 0 bytes received`; no `*` lines in `-v`; progress total 0.

**Where to start.** `Curl.Core.UnitLibrary/MaxTimeWatchdog.cs` (about lines 193-199) says "Connection timed out" until the handler reports the transfer started; `Curl.Original.Protocol.Smb.UnitLibrary/SmbFileTransfer.cs` calls `ReportTransferStarted()` only after the open (about line 130) and passes `file.EndOfFile` only with the first data (line 152). Mark the transfer started once TCP/TLS is up and report the expected size right after NT_CREATE. Other protocols share the watchdog: keep their messages unchanged. BL-5328 also changes `SmbFileTransfer.cs`; the two never run at once.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Smb.UnitTests/SmbTimeoutMessageTests.cs` (fake clock, stand-in server that stalls) prove: a stall at SESSION_SETUP yields exit 28 with `Operation timed out after 3000 milliseconds with 0 bytes received`; a stall at READ after an open with EndOfFile 10 yields `... with 0 out of 10 bytes received`, the `-v` `* ` lines named above, and a progress total of 10.
- [ ] `Curl.Core.UnitTests/MaxTimeWatchdogTests.cs` still pins the existing "Connection timed out" text for a handler that has not reported a start, so no other protocol's message changes (add a case there if a watchdog change needs one).
- [ ] Tests are platform-neutral (stand-ins, fake `TimeProvider`).
- [ ] Hand check, recorded in the Log: repro (a) and (b) print the expected text; the Log says whether real curl 8.18 was measured with `Record-CurlExchange.ps1` or the review's text was used.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: (a) prints `Operation timed out after 3000 milliseconds with 0 bytes received`; (b) prints `with 0 out of 10 bytes received`.
- `SmbSessionEstablisher.cs` is in `touches` in case the started-mark belongs there; drop it if unneeded.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 6705f074 ba7d49e0 72f8a05b 27561605 a0b49a29.
