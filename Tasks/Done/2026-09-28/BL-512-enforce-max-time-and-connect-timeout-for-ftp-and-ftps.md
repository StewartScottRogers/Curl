---
id: BL-512
title: Enforce --max-time and --connect-timeout for ftp and ftps
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-498, BL-510, BL-511]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-512 — Enforce --max-time and --connect-timeout for ftp and ftps

## Goal

An FTP or FTPS transfer that outlasts `-m`, or whose control or data connection outlasts `--connect-timeout`, ends with exit 28 and the message curl 8.21.0 prints in that phase (including a data connection curl waits to accept in active mode, exit 12 where curl gives it).

## Context

- Conformance audit 2026-09-28, row 12 (Blocker). Mechanism: BL-498's ADR; connect phase: BL-510.
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs` and the handler; routing in `Curl.Console/RoutingFtpProtocolHandler.cs`. Active mode waits on `IConnectionListener` (ADR-0102).
- `Record-CurlExchange.ps1 -Ftp` serves FTP; add a stall option to it (for instance delay one named reply) if it has none, rather than writing another server.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp` (extended if needed): `-m 1` with the server stalling after the greeting, after `PASV`, and mid-`RETR`; `--connect-timeout 1` with a data connection that never connects; active mode (`-P -`) where the server never connects back; stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` tests on a fake `TimeProvider` pin each measured exit code and message.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Ftp.UnitLibrary` and `Curl.Console`.

## Notes

Measured 2026-09-28, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Ftp` (extended:
the reply `STALL` answers nothing, `-FtpDataHoldMilliseconds` holds a RETR data connection open).
All exit 28; `-v` also prints `* <message>` before `* closing connection #0`.
- `-m 1`, `GREETING=STALL`: `curl: (28) Operation timed out after 1008 milliseconds with 0 bytes received`.
- `--connect-timeout 1`, `GREETING=STALL`: `curl: (28) Operation timed out after 1011 milliseconds with 0 bytes received`.
- `-m 1` and `--connect-timeout 1`, `USER=STALL`: `Operation timed out after 1005 milliseconds with 0 bytes received`.
- `-m 1 --disable-epsv`, `PASV=STALL`: `Operation timed out after 1002 milliseconds with 0 bytes received` (EPSV the same, 1012 ms).
- `-m 1`, `SIZE=213 100`, 5-byte FtpData held 3 s: `Operation timed out after 1005 milliseconds with 5 out of 100 bytes received` (`-sS` the same, 1015 ms).
- `-m 1 --disable-epsv --no-ftp-skip-pasv-ip`, PASV naming 10.255.255.1: `Operation timed out after 1013 milliseconds with 0 bytes received`.
- The same with `--connect-timeout 1` and no `-m`: curl does NOT hold the data connect to it; after
  Windows' 21 s SYN timeout, `curl: (28) Failed to connect to 127.0.0.1:47911 via 10.255.255.1:1025 after 21103 ms: Could not connect to server`.
  `--connect-timeout 2 -m 5`: `Operation timed out after 5012 milliseconds with 0 bytes received`.
- `-P - -m 1`, RETR answered 150 and no connection back: `* Data conn was not available immediately`, then
  `Operation timed out after 1004 milliseconds with 0 bytes received` (exit 28, not 12: the multi loop's `-m`
  wins). `--connect-timeout 1 -m 4`: `after 4017 milliseconds` - the accept wait ignores `--connect-timeout`.
  Exit 12 `Accept timeout occurred while waiting server connect` after 60 s with no `-m` is BL-437's
  measurement, already pinned (`ExecuteAsync_ServerNeverConnects_QuitsWithExit12After60Seconds`).

What this shows, and what was built (the mechanism is ADR-0117; decided by Claude under Stewart's delegation):
- curl's `-m` and connect-timeout message after the TCP connect is always `Operation timed out ...`, never
  `Connection timed out`. The runner's `MaxTimeWatchdog` picks the wording from `ReportTransferStarted`, which FTP
  only reported once the data connection was ready. `FtpProtocolHandler` now reports it as soon as the control
  connection is up (the `ITransferProgress` contract's "past connect"), and the two later calls are gone. This also
  matches curl's progress meter, whose header was measured to appear right after `Established connection`.
- curl holds its states before `DO` (greeting, `AUTH`, `USER`, `PASS`, `PBSZ`, `PROT`, `PWD`) to `--connect-timeout`,
  counted from the request's start, with the Operation message. The connector's limit ends with the TCP connect, so
  the new `FtpConnectPhaseLimit` holds that phase: a timer on the context's `TimeProvider` (re-armed if it fires
  early, like `MaxTimeWatchdog`) cancels a token linked with the transfer's, which `FtpControlChannel` and the
  `AUTH` handshake use until `PWD` is answered; then the channel switches back to the transfer's token. 300 s when
  `--connect-timeout` is absent or 0 (curl's `DEFAULT_CONNECT_TIMEOUT`). A cancellation before the limit passed
  (`-m`) still escapes to the runner.
- Every other wait already passed `ITransferContext.CancellationToken` and let `OperationCanceledException` out, and
  the active-mode accept keeps its 60 s exit 12, bounded by `-m` through the token, as measured.
- Not pinned here: the data connect's OS timeout message and curl not holding it to `--connect-timeout`. That is
  the connector's (`TcpConnector` holds every connect to its limit), so it is filed as BL-797.
- ADR-0117 could not take an amendment here: `Documentation/Planning/Decisions` is in BL-617's `touches`, which is in
  Doing. Filed as BL-798 (docs); this Notes section and the FTP project's CLAUDE.md record the decision meanwhile.
- `touches` gained `Record-CurlExchange.ps1` (the task's Context asks for the stall option; no task in Doing names it).
- Tests: `FtpProtocolHandlerTimeLimitTests` (11, on a stepping fake clock: greeting and `USER` stalls at
  `--connect-timeout`, 999 ms still running, 300 s default for none and 0, an early timer, a slow TCP connect, no
  limit after `PWD`, `-m` cancellation escaping during login, transfer started before the greeting, mid-RETR
  progress 5 of 100); `CurlCommandRunnerFtpTimeLimitTests` (5, end to end through the runner: no greeting, `USER`
  at `--connect-timeout`, EPSV, a stalled data connect, mid-RETR `5 out of 100`). The active-mode accept test now
  checks the last timer wait, since the connect-phase timer comes first.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ftp and ftps stalls after the TCP connect end with exit 28 and curl's Operation timed out message under -m, and --connect-timeout now holds the greeting, login and PWD
