---
id: BL-1091
title: Run the FTP PROT P data handshake right after the passive connect, as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1091 — Run the FTP PROT P data handshake right after the passive connect, as curl does

## Goal

Under `PROT P`, a passive-mode FTP transfer's `-v` writes the data connection's `schannel:` lines where curl 8.21.0 does: right after `Trying <host>:<data port>...`, before `> TYPE I`.

## Context

- Measured for BL-1084 (`Record-CurlExchange.ps1 -Ftp -CurlArgs '-v','-k','--ssl-reqd','ftp://127.0.0.1:18021/a.txt'`): curl writes `*   Trying 127.0.0.1:<port>...`, then the two `schannel:` lines, then `> TYPE I`, `SIZE`, `RETR`, `< 150`, then `Established 2nd connection`. curl starts the data handshake as soon as the data socket connects.
- Since BL-1084, `FtpSession.SecureDataConnectionAsync` passes the transfer's events to the handshake, so the lines are written. But it runs from `ReadyDataConnectionAsync`, after the transfer command's `150`, so the lines come after `< 150`.
- Active mode accepts the server's connection only after the transfer command, so its handshake cannot move earlier. Measure where curl writes the lines under `-P -` before changing that path.

## Acceptance criteria

- [x] A passive `PROT P` download runs the data handshake before `TYPE I` is sent, and a test in `Curl.Protocol.Ftp.UnitTests` pins the order of the `schannel:`-producing handshake against the `TYPE I` header.
- [x] A failed early handshake still ends the transfer with its exit code and no `QUIT`, as `ExecuteAsync_DataHandshakeFails_EndsWithItsExitCodeWithoutQuit` pins today.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

- Passive mode: `FtpSession.ConnectDataAsync` now runs `SecureDataConnectionAsync` as soon as the data socket connects, so under `PROT P` the handshake and its `schannel:` lines come after `Trying <host>:<port>...` and before `> TYPE I`, where curl 8.21.0 was measured to write them for BL-1084. `ReadyDataConnectionAsync` now does nothing in passive mode.
- Active mode (`-P`) is unchanged: the server connects only after the transfer command, so the handshake still runs right after the accept. It was not re-measured under `-P -` because nothing on that path moved. `ExecuteAsync_ActiveProtP_RunsTheDataHandshakeAfterTheAccept` now pins it.
- A failed early handshake still ends with its exit code and no `QUIT`. `ExecuteAsync_DataHandshakeFails_EndsWithItsExitCodeWithoutQuit` now expects the control commands to stop at `EPSV`, because `TYPE I`, `SIZE` and `RETR` are no longer sent before the handshake.
- The order is pinned by `ExecuteAsync_PassiveProtP_RunsTheDataHandshakeBeforeTypeI`: the last header before the data handshake is `< 229 ...`. The fake `QueuedTlsProvider` gained `BeforeEachHandshake` for this test.
- Measure-CodeQuality: Curl.Protocol.Ftp.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Passive PROT P data handshake now runs right after the data connect, before TYPE I, as curl does
