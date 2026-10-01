# ADR-0286 — An FTP passive data connect is not held to `--connect-timeout`, and a dial the system timed out is exit 28

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-797.
Amends ADR-0117 (the connector holds every connect to `--connect-timeout`).

## Context

ADR-0117 made `TcpConnector` hold every connect, FTP's passive data connection included, to
`--connect-timeout` (or a smaller `-m`). curl 8.21.0 does not hold the data connection to it.
Measured 2026-09-30 with `Record-CurlExchange.ps1 -Ftp -FtpReply 'PASV=227 Entering Passive Mode
(10,255,255,1,4,1)'` and `-v --disable-epsv --no-ftp-skip-pasv-ip --connect-timeout 1
ftp://127.0.0.1:47911/f.txt` (BL-797 Notes):

- curl 8.21.0 (mingw, Schannel) waited 21 s, Windows' own SYN retries, printed `* connect to
  10.255.255.1 port 1025 from 0.0.0.0 port 51785 failed: Timed out`, then `curl: (28) Failed to
  connect to 127.0.0.1:47911 via 10.255.255.1:1025 after 21125 ms: Could not connect to server`.
- curl 8.18.0 (Ubuntu, OpenSSL), the only Linux build available (ADR-0009), ended the data connect
  at 1 s with `curl: (28) Connection timeout after 1002 ms`.
- A plain dial the system gives up on is exit 28 too, not exit 7, with the connect message:
  `curl http://10.255.255.1:1025/` is `curl: (28) Failed to connect to 10.255.255.1:1025 after
  21047 ms: Could not connect to server` on 8.21.0 (Schannel), and `... 10.255.255.1 port 1025
  after 134182 ms: Could not connect to server` (exit 28) on 8.18.0 (OpenSSL).

## Decision

1. FTP's passive data connections go through a connector with no connect limit: `Curl.Console`
   gives `FtpProtocolHandler` (its new `dataConnector` constructor parameter) the option group's
   `PoolingConnector.Over(TcpConnector.WithoutConnectTimeout())`, which shares the pool's cache and
   numbering and the TCP connector's DNS cache, `--resolve`, `--connect-to` and every other
   setting. Only `-m` (the runner's `MaxTimeWatchdog`, whose `Operation timed out` message BL-512
   pinned) or the system ends it. The control connection keeps ADR-0117's limit.
2. `WithoutConnectTimeout` runs the connect under the longest delay a .NET timer takes, about 49.7
   days, rather than adding a branch for "no limit".
3. On every connect, a dial whose last attempt failed with `SocketError.TimedOut` is exit 28
   (`CurlExitCode.OperationTimedOut`) with the same `Failed to connect to ... after N ms: Could not
   connect to server` message; the FTP handler names the control connection and `via` the data
   address in it, as BL-904 already did.
4. The 8.21.0 behaviour is taken on every platform. The Linux measurement is an older version
   (8.18.0), whose other wording (`Connection timeout after`, `host port N`) the project already
   does not follow; the limit is applied by libcurl's platform-independent timeout code, so the
   difference is the version's, not the platform's.

## Consequences

- `ftp://` with `--connect-timeout` and an unreachable passive address runs until the system gives
  up (about 21 s on Windows, about 2 minutes on Linux) or `-m` ends it, as curl 8.21.0 does.
- `%{exitcode}` and `--retry` see 28 rather than 7 for a connect the system timed out.
- A test that swaps `CurlTransports.PoolingConnector` for a scripted one still sends FTP data
  connections through the transports' real `TcpConnector`; such a test passes its own
  `ftpDataConnector` to `CurlComposition.CreateProtocolHandlers`.
