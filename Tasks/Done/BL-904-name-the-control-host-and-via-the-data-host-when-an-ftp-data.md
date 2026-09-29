---
id: BL-904
title: Name the control host and via the data host when an FTP data connection fails to connect
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-904 — Name the control host and via the data host when an FTP data connection fails to connect

## Goal

A failed FTP data connection ends with curl 8.21.0's message, which names the control connection's host and port and then the data address: `Failed to connect to <control host>:<control port> via <data host>:<data port> after <n> ms: Could not connect to server`.

## Context

- Found in BL-662. Measured with `Record-CurlExchange.ps1 -Ftp -FtpReply 'EPSV=500 no' 'PASV=227 Entering Passive Mode (127,0,0,2,0,1)'` and `--no-ftp-skip-pasv-ip`: `curl: (7) Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2041 ms: Could not connect to server`; with an unroutable `10.255.255.1`, exit 28 with the same shape after the SYN timeout; with the default `--ftp-skip-pasv-ip` and port 1, `via 127.0.0.1:1`.
- Today `FtpSession.ConnectDataAsync` passes the connector's message through, which names only the data host. `Curl.Networking.UnitLibrary/TcpConnector.cs` already writes the `via` form for a mapped destination (`ConnectDestination.IsMapped`); find whether the FTP handler can ask for it through `ConnectTarget` (Abstractions) or should rewrite the message itself, and keep the change within FTP if it can.

## Acceptance criteria

- [x] Measured first, including `-v` for the refused case; stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin the refused (exit 7) and timed-out (exit 28) messages.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured with curl 8.21.0 (Schannel, mingw64) on 2026-09-29:

- `-Port 47707 -Ftp -FtpReply 'EPSV=500 no','PASV=227 Entering Passive Mode (127,0,0,2,0,1)'`,
  `curl -v --no-ftp-skip-pasv-ip ftp://127.0.0.1:47707/f.txt`: exit 7; the `-v` tail is
  `* Connecting to 127.0.0.2 port 1`, `*   Trying 127.0.0.2:1...`,
  `* connect to 127.0.0.2 port 1 from 0.0.0.0 port 63303 failed: Connection refused`,
  `* Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could not connect to server`,
  `* shutting down connection #0`, then
  `curl: (7) Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could not connect to server`.
- PASV naming `10.255.255.1` port 1, `-sS --connect-timeout 2 --no-ftp-skip-pasv-ip ftp://127.0.0.1:47708/f.txt`:
  exit 28, `curl: (28) Failed to connect to 127.0.0.1:47708 via 10.255.255.1:1 after 21175 ms: Could not connect to server`
  (`--connect-timeout` did not shorten the data connect's SYN timeout).
- PASV naming `127.0.0.2` port 1, default `--ftp-skip-pasv-ip`, `-sS ftp://localhost:47709/f.txt`:
  exit 7, `curl: (7) Failed to connect to localhost:47709 via 127.0.0.1:1 after 2263 ms: Could not connect to server`.
  So the part before `via` is the URL's host and port as the control connect named them, and
  the part after is the address `-v` names in `Connecting to` (the control peer's address, or
  the `227` address under `--no-ftp-skip-pasv-ip`).

Decision (the task's own preference, keep it in FTP): the FTP handler rewrites the message
itself rather than asking `TcpConnector` for its mapped `via` form through `ConnectTarget`,
which would have widened the task into Abstractions and Networking. `FtpDataConnectFailure`
rewrites a message that begins `Failed to connect to <dialled host>:<port> after ` and leaves
any other (a resolve failure, a proxy's) untouched; `FtpDataConnectEvents` wraps the
transfer's events for the data connect so the connector's `-v` copy of the line is rewritten
the same way. The test fake `QueuedConnector` now records targets without their events
(record equality would otherwise compare the wrapper) and can report events on the data
connect (`DataConnectReports`).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A failed FTP data connection names the control host and port, then via the data address, as curl 8.21.0 does, on stderr and in -v
