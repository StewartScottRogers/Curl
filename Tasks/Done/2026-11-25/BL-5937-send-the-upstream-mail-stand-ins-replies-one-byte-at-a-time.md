---
id: BL-5937
title: Send the upstream mail stand-ins' replies one byte at a time under SLOWDOWN
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5924]
touches: [Curl.Conformance.UnitLibrary/LineProtocolServerConnector.cs, Curl.Conformance.UnitLibrary/Pop3ServerConnector.cs, Curl.Conformance.UnitLibrary/ImapServerConnector.cs, Curl.Conformance.UnitLibrary/SmtpServerConnector.cs, Curl.Conformance.UnitLibrary/FtpServerConnector.cs, Curl.Conformance.UnitLibrary/OneBytePerReadConnection.cs, Curl.Conformance.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitTests/Pop3ServerConnectorTests.cs, Curl.Conformance.UnitTests/ImapServerConnectorTests.cs, Curl.Conformance.UnitTests/SmtpServerConnectorTests.cs, Curl.Conformance.UnitTests/FtpServerConnectorTests.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5937 — Send the upstream mail stand-ins' replies one byte at a time under SLOWDOWN

## Goal

For a POP3, IMAP or SMTP case whose `<servercmd>` says `SLOWDOWN`, the conformance harness's mail stand-in gives curl its greeting and each reply one byte per read, as upstream's `ftpserver.pl` does.

## Context

BL-5924 did this for the FTP stand-in only: `FtpServerConnector` wraps each control connection in `OneBytePerReadConnection` (10 ms a byte) when `<servercmd>` has a `SLOWDOWN` line. Upstream's `ftpserver.pl` serves the mail protocols too, and test 853 (`pop3`, on `PassingUpstreamCases.txt`) says `SLOWDOWN`, which `Pop3ServerConnector` ignores. Move the `SLOWDOWN` line check and the wrapping where the four stand-ins share them (`LineProtocolServerConnector`). Waits go on each stand-in's injected clock.

## Acceptance criteria

- [x] A unit test in `Curl.Conformance.UnitTests` shows a `SLOWDOWN` POP3, IMAP and SMTP server's replies arriving one byte per read, with their waits on the injected clock.
- [x] Every `SLOWDOWN` case on `PassingUpstreamCases.txt` still passes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `LineProtocolServerConnector` now takes the case's `<servercmd>` body, holds the `SLOWDOWN`
  line check, `SlowReplyByteDelay` and the `OneBytePerReadConnection` wrapping;
  `FtpServerConnector` lost its own copy. The dict stand-in passes no `<servercmd>`.
- The three mail stand-ins take an optional `TimeProvider`, and the runner gives them its
  server clock, so `UpstreamCaseRunner.cs` was added to `touches` (no task in Doing named it,
  nor `FtpServerConnectorTests.cs`, which follows the moved delay property). Their `DELAY`
  waits therefore go on the server clock too, as the FTP stand-in's already did: skipped
  unless a curl timer races the server (ADR-0404), instead of on the system clock.
- Tests: one `ConnectAsync_SlowDownServer_GivesEachReplyOneBytePerReadAfterAWaitOnTheClock`
  in each of the three connector test classes. The listed `SLOWDOWN` cases are 250, 251 and
  853; the conformance ratchet passed with them (4382 passed, 0 failed).
- `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: line 100, branch 100,
  no failing member, worst CRAP 10.
- No test touches a file, path or platform error, so nothing differs by platform.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 2c5471be 1201ce2f 3d836d5e.
- 2026-11-25: Doing -> Done. The POP3, IMAP and SMTP stand-ins send their greeting and replies one byte at a time under SLOWDOWN, waiting on the runner's server clock.
