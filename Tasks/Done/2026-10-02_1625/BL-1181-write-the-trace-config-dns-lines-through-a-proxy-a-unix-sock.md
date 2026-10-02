---
id: BL-1181
title: Write the --trace-config dns lines through a proxy, a Unix socket, a negative cache entry and after an async resolve
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1157]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1181 — Write the --trace-config dns lines through a proxy, a Unix socket, a negative cache entry and after an async resolve

## Goal

Under `-v --trace-config dns` Curl writes curl 8.21.0's `[DNS]` lines for a connect through a proxy or Unix socket, for a negative DNS cache entry, and the `resolve complete`/`async` teardown lines after a name that resolved; split out of BL-1157.

## Context

- Measured in BL-1157 Notes: `-x http://127.0.0.1:P` writes the same filter lines as a direct connect, for the proxy's host and port (`TcpConnector.TracingConnectionFilters` today wraps only the direct path).
- A `--resolve` entry left empty by `-6` (cache negative): `[DNS] created DNS filter for foo:P, transport=3, queries=2`, ..., `[DNS] cache entry does not have type=AAAA addresses`, `Negative DNS entry`, `Could not resolve host: foo`, `Could not resolve: foo:P`, `Could not resolve: foo`, `[DNS] error resolving: 6`, `-> 6, done=0`, `filter returned 6`, `closing connection #0` (no async lines). Plain `-v` has the three `Could not resolve` lines too.
- After a DoH resolve that succeeded and a refused dial: `[DNS] resolve complete for example.test:P` before `Host ... was resolved.`, and `[DNS] [1] shutdown async` / `[DNS] [1] destroy async` around `closing connection #0` (`AsyncResolveTeardownTraceEvents` handles the destroy line).
- Measure a Unix socket (`--unix-socket`) with `Record-CurlExchange.ps1` first.

## Acceptance criteria

- [x] Measured first: the Unix socket case, stderr in Notes.
- [x] `Curl.Console.UnitTests` pin the proxy, negative-cache and resolved-then-refused lines as measured in BL-1157 Notes, and the Unix socket's as measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), refused targets run directly (no server needed):

- `-s -v --trace-config dns --unix-socket /tmp/nonexistent.sock http://example.test/` (exit 7):
  `[DNS] created DNS filter for C:/Users/.../Temp/nonexistent.sock:0, transport=6, queries=3`, `[DNS] added`,
  `[DNS] cf_dns_start unix-domain-socket C:/.../nonexistent.sock:0`, `  Trying C:/.../no:0...`,
  `Immediate connect fail for C:/.../no: Connection refused`, `connect to C:/.../no port 0 from  port 0 failed: Connection refused`,
  `Failed to connect to example.test:80 over unix://C:/.../nonexistent.sock after 0 ms: Could not connect to server`,
  `[DNS] Curl_conn_connect(block=0) -> 7, done=0`, `[DNS] Curl_conn_connect(), filter returned 7`, `closing connection #0`.
  (The MSYS shell turned `/tmp` into the Windows path; curl writes the path as given.) No `done=0` after `Trying`.
- `-p -x http://127.0.0.1:47115 http://example.test/`: the same filter lines for `127.0.0.1:47115` as `-x`, ending
  `Failed to connect to example.test:80 over proxy 127.0.0.1 ...` and the exit 7 lines.
- `-4 --resolve foo:47500:::1`: `queries=1`, `[DNS] cache entry does not have type=A addresses`.
- Plain `-v -6 --resolve foo:47500:127.0.0.1`: `Added ...`, `Negative DNS entry`, `Could not resolve host: foo`,
  `Could not resolve: foo:47500`, `Could not resolve: foo`, `closing connection #0`.
- `http://localhost:47199/` (refused): no `resolve complete`, no async lines - curl answers localhost itself.
- `http://<this machine's name>:47199/` (system resolver, refused): `[DNS] resolve complete for H:47199` before
  `Host H:47199 was resolved.`, and `[DNS] [1] shutdown async`, `closing connection #0`, `[DNS] [1] destroy async`
  after the exit 7 lines - the same as the DoH case, so it applies to every looked-up name.

Decisions (ADR-0381): `DnsFilterTraceEvents` keys on the connector's own `Negative DNS entry`, `Hostname ... was
found in DNS cache` and `Host H:P was resolved.` lines. A tunnelling proxy is traced by the DNS filter alone, for its
first hop. A Unix socket's success lines and a looked-up name's success teardown were not measured: the success
writes the host's connected-chain lines and no shutdown. The two existing DoH composition tests gained the measured
`resolve complete` line.

Coverage: `Curl.Networking.UnitLibrary` 100% line and branch, no failing member. `Curl.Console` 100% line and branch.
It was not changed, and its one complexity flag (`CurlCommandRunner.TransferUrlAsync`, 12) was there before this task.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config dns writes curl's DNS filter lines through a proxy, over a Unix socket, on a negative cache entry and after a looked-up name's refused dial
