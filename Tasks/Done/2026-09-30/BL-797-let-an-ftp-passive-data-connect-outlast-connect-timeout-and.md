---
id: BL-797
title: Let an FTP passive data connect outlast --connect-timeout and report curl's via message
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-512]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-797 — Let an FTP passive data connect outlast --connect-timeout and report curl's via message

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

- [x] Measured first on the Schannel build and the Linux build; stderr and exit code copied into Notes.
- [x] A test on a fake `TimeProvider` shows a passive data connect still running past `--connect-timeout 1` with no `-m` is not ended by it.
- [x] A test pins exit 28 and the measured `Failed to connect to ... via ...: Could not connect to server` message for a data dial the system times out.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measurements (2026-09-30)

- **Schannel, curl 8.21.0 (mingw):** `Record-CurlExchange.ps1 -Port 47911 -Ftp -FtpReply 'PASV=227 Entering Passive Mode (10,255,255,1,4,1)' -FtpIdleMilliseconds 30000 -CurlArgs '-v','--disable-epsv','--no-ftp-skip-pasv-ip','--connect-timeout','1','ftp://127.0.0.1:47911/f.txt'`
  exited 28 after 21418 ms. stderr (progress lines trimmed):
  `* Connecting to 10.255.255.1 port 1025`, `*   Trying 10.255.255.1:1025...`,
  `* connect to 10.255.255.1 port 1025 from 0.0.0.0 port 51785 failed: Timed out`,
  `* Failed to connect to 127.0.0.1:47911 via 10.255.255.1:1025 after 21125 ms: Could not connect to server`,
  `* shutting down connection #0`,
  `curl: (28) Failed to connect to 127.0.0.1:47911 via 10.255.255.1:1025 after 21125 ms: Could not connect to server`.
- **Schannel, plain dial:** `curl -sS http://10.255.255.1:1025/` -> exit 28,
  `curl: (28) Failed to connect to 10.255.255.1:1025 after 21047 ms: Could not connect to server`.
  So a dial the system times out is exit 28 on every connect, not only FTP's; `TcpConnector`
  returned exit 7 for it.
- **Linux, curl 8.18.0 (Ubuntu, OpenSSL, WSL 2):** Windows' firewall kept WSL off the recorder's
  listener on 172.26.96.1 (the control connect itself timed out at 1 s), so the FTP server ran inside
  WSL: a bash loop over `nc -l` answering 220/331/230/257/227 (no Python). `curl -v --disable-epsv
  --no-ftp-skip-pasv-ip --connect-timeout 1 ftp://127.0.0.1:47914/f.txt` ended at 1 s:
  `* Connection timeout after 1002 ms`, `* Failed to connect to 127.0.0.1 port 1025 after 1002 ms: Timeout was reached`,
  `curl: (28) Connection timeout after 1002 ms`. Plain dial without `--connect-timeout`:
  `curl: (28) Failed to connect to 10.255.255.1 port 1025 after 134182 ms: Could not connect to server`;
  with `--connect-timeout 1`: `curl: (28) Connection timed out after 1001 milliseconds`.

### Decisions (ADR-0286, decided by Claude under Stewart's delegation)

- The 8.21.0 behaviour everywhere: the Linux build is 8.18.0 and its wording already differs from
  8.21.0's in ways the project does not follow; the limit is libcurl's platform-independent timeout
  code, so the difference is the version's.
- Design: `TcpConnector.WithoutConnectTimeout()` (a view sharing DNS cache, numbering and settings,
  its connect under the longest timer delay, ~49.7 days, so no extra branch) and
  `PoolingConnector.Over(inner)` (same cache, configuration and numbering). `CurlComposition.FtpDataConnectorOf`
  wires `PoolingConnector.Over(TcpConnector.WithoutConnectTimeout())` into a new `FtpProtocolHandler`
  constructor with a `dataConnector`; `CreateProtocolHandlers` takes it as an optional
  `ftpDataConnector`. `-m` still ends a data connect through the runner's watchdog (BL-512's pin
  unchanged). ConnectTarget (Abstractions) was left alone so the task need not touch the shared contract.
- `TcpConnector.DialFailure`: last error `SocketError.TimedOut` -> exit 28, message unchanged.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0286 and its README row; no task in
  Doing named it (only BL-1008, Audit tools).

### Tests

- `TcpConnectorTests.WithoutConnectTimeout`: a stalled dial through the view is still running 60 s
  past `--connect-timeout 1` on a `ManualTimeProvider`; numbering shared; system `TimedOut` -> exit 28.
- `PoolingConnectorSharedCacheTests.Over_*`, `FtpProtocolHandlerDataConnectFailureTests` (data connector
  used, exit 28 and the measured via message, null guard), `CurlCommandRunnerFtpTimeLimitTests.RunAsync_DataConnectStillRunningPastTheConnectTimeout_RunsOnUntilTheSystemGivesUp`,
  `CurlCompositionTests.*FtpDataConnectionsGoThroughThePoolOverAConnectorWithoutTheConnectTimeout`.
- `Measure-CodeQuality.ps1`: Curl.Networking.UnitLibrary, Curl.Protocol.Ftp.UnitLibrary and Curl.Console
  each 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. an ftp passive data connect is bounded only by -m and the system, ending with exit 28 and curl's via message; a system-timed-out dial is exit 28
