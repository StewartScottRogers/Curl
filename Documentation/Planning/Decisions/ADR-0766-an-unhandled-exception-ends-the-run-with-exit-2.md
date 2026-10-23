# ADR-0766 — An unhandled exception ends the run with exit 2

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3172
- Decided by Claude under Stewart's delegation.

## Context

`curl -s http://127.0.0.1:0/` killed Curl with `Unhandled exception. System.ArgumentOutOfRangeException`
and exit code 0xC0000409, because `ConnectTarget` refused port 0. curl 8.22.0 (Git for Windows' ucrt64
build), measured on 2026-10-22 with `Record-CurlExchange.ps1 -NoServer`, exits 7 for every port 0 case:

| Command line | stderr |
| --- | --- |
| `http://127.0.0.1:0/`, `https://127.0.0.1:0/`, `ftp://127.0.0.1:0/` | `curl: (7) Failed to connect to 127.0.0.1:0 after 0 ms: Could not connect to server` |
| `http://localhost:0/` | `curl: (7) Failed to connect to localhost:0 after 0 ms: Could not connect to server` |
| `http://[::1]:0/` | `curl: (7) Failed to connect to ::1:0 after 0 ms: Could not connect to server` |
| `-x http://127.0.0.1:0 http://example.test/` | `curl: (7) Failed to connect to 127.0.0.1:0 over proxy 127.0.0.1 after 0 ms: Could not connect to server` |
| `-x http://127.0.0.1:0 https://example.test/` | `curl: (7) Failed to connect to example.test:443 over proxy 127.0.0.1 after 0 ms: Could not connect to server` |
| `-x socks5://127.0.0.1:0 http://example.test/` | `curl: (7) Failed to connect to example.test:80 over proxy 127.0.0.1 after 0 ms: Could not connect to server` |

Any other bug that lets an exception escape `Program.Main` still kills the process with no `curl: (N)` line.

## Decision

- `ConnectTarget` and `ProxyEndpoint` accept port 0, `ProxyUrlParser` no longer fails it early, and
  `TcpConnector` fails a dial to port 0 without opening a socket (`SocketError.ConnectionRefused`), so the
  connector's own failure messages give each line above.
- `Program.Main` runs the command line through `UnhandledExceptionGuard`: an exception escaping it, other
  than an `OperationCanceledException` (which keeps its handling), flushes standard output, writes
  `curl: (2) <message>` to standard error and gives exit 2, `CURLE_FAILED_INIT`.

Exit 2 because upstream curl has no exception to measure: an internal failure it cannot otherwise name is
reported as `CURLE_FAILED_INIT` ("Very early initialization code failed. This is likely to be an internal
error or problem"), while 4 (`CURLE_NOT_BUILT_IN`) claims a feature is missing and 8
(`CURLE_WEIRD_SERVER_REPLY`) blames the server, neither true of a bug in Curl.

## Consequences

A script never sees a crash code; a bug shows up as exit 2 with its message, which is enough to file it.
The guard uses no reflection, so native AOT is unaffected.
