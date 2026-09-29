---
id: BL-795
title: Let an FTP passive data connect outlast --connect-timeout and report curl's via message
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-512]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-795 — Let an FTP passive data connect outlast --connect-timeout and report curl's via message

## Goal

An `ftp://` passive data connection that never connects is bounded only by `-m` and the operating system's own connect timeout, not by `--connect-timeout`, and when the system gives up it ends with exit 28 and `Failed to connect to <control host>:<control port> via <data address>:<data port> after N ms: Could not connect to server`, as curl 8.21.0 does.

## Context

- Found in BL-512 (see its Notes). Measured 2026-09-28 with curl 8.21.0 (mingw, Schannel):
  `Record-CurlExchange.ps1 -Port 47911 -Ftp -FtpReply 'PASV=227 Entering Passive Mode (10,255,255,1,4,1)' -FtpIdleMilliseconds 8000 -CurlArgs -v,--disable-epsv,--no-ftp-skip-pasv-ip,--connect-timeout,1,ftp://127.0.0.1:47911/f.txt`
  waited 21 s (Windows' SYN retries), printed `* connect to 10.255.255.1 port 1025 from 0.0.0.0 port 63889 failed: Timed out`, then
  `curl: (28) Failed to connect to 127.0.0.1:47911 via 10.255.255.1:1025 after 21103 ms: Could not connect to server`.
  With `-m 1` instead it ended at 1 s with `Operation timed out after 1013 milliseconds with 0 bytes received` (pinned by BL-512).
- Today `TcpConnector` holds every connect, the data connection's included, to the smaller of `--connect-timeout` and `-m` (ADR-0117 and its BL-510 amendment), so this data connect ends at 1 s with `Connection timed out after 1000 milliseconds`.
- The FTP handler gets its data connection from the same `IConnector` as the control connection (`FtpSession.ConnectDataAsync`, `CurlComposition.CreateFtpProtocolHandler`). One way: a second connector for data connections built without the connect limit; the "via" wording is the control host and port with the data address as the mapped destination (`TcpConnector`'s existing `via` form for `--connect-to`).
- Measure the Linux (OpenSSL) build too (`-ListenAddress` with `-Curl wsl.exe`, BL-474) before pinning; its system timeout is longer.

## Acceptance criteria

- [ ] Measured first on the Schannel build and the Linux build; stderr and exit code copied into Notes.
- [ ] A test on a fake `TimeProvider` shows a passive data connect still running past `--connect-timeout 1` with no `-m` is not ended by it.
- [ ] A test pins exit 28 and the measured `Failed to connect to ... via ...: Could not connect to server` message for a data dial the system times out.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
