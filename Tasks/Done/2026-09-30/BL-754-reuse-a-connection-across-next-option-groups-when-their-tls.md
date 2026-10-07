---
id: BL-754
title: Reuse a connection across -:/--next option groups when their TLS and proxy settings match
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-509]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-754 — Reuse a connection across -:/--next option groups when their TLS and proxy settings match

## Goal

A later `-:`/`--next` option group reuses an idle connection an earlier group left in the pool when curl 8.21.0 would (same host, port, scheme, proxy and matching TLS configuration), so `-v` prints `Re-using existing connection` and `%{num_connects}` is `0` for it, as upstream does.

## Context

- BL-509 / ADR-0126: each option group gets its own `TransferDispatch`, so its own `PoolingConnector`; connections are never reused across groups. curl 8.21.0 shares one connection cache across all groups (the tool's share handle locks `CURL_LOCK_DATA_CONNECT`) and reuses a connection only when `Curl_ssl_config_matches` and the proxy settings agree.
- Measured in BL-509 Notes: `-w '[%{conn_id} %{num_connects}]' A --next -w ... B` against a server that kept the connection open sent `GET /b` on A's connection first.
- `Curl.Networking.UnitLibrary/ConnectionPoolKey.cs` leaves the TLS options out of the key because "one run has one set"; with groups that is no longer true. `Curl.Console/CurlComposition.CreateTransports` builds the `TcpConnector` from one group's options.
- Start: one run-wide pool whose key carries the TLS and proxy-tunnel configuration, and a per-group inner connector.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -HoldOpenMilliseconds` (extend it to answer a second request on a held connection if needed): same settings in both groups, and `-k` in only one; `-v` lines, `%{num_connects}` and the request bytes copied into Notes.
- [x] `Curl.Console.UnitTests` pin that a second group with the same settings reuses the first group's connection and one with a different TLS setting opens a new one.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Console` and `Curl.Networking.UnitLibrary` and no failing member.

## Notes

### Measured 2026-09-30, curl 8.21.0 (x86_64-w64-mingw32, Schannel)

`Record-CurlExchange.ps1` gained `-AnswerHeldRequests N`: with `-HoldOpenMilliseconds`, up to N
more requests arriving on a held connection are answered with the next responses. Every run
below used `-Connections 2 -HoldOpenMilliseconds 3000 -AnswerHeldRequests 1`, response
`HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok`.

1. Same settings, plain HTTP: `-v -w '[%{num_connects} %{conn_id}]\n' http://127.0.0.1:18754/a --next -v -w ... http://127.0.0.1:18754/b`.
   Exit 0, stdout `ok[1 0]` / `ok[0 0]`. `-v` `*` lines:
   ```
   *   Trying 127.0.0.1:18754...
   * Established connection to 127.0.0.1 (127.0.0.1 port 18754) from 127.0.0.1 port 50560
   * using HTTP/1.x
   * Request completely sent off
   * Connection #0 to host 127.0.0.1:18754 left intact
   * Reusing existing http: connection with host 127.0.0.1
   * Request completely sent off
   * Connection #0 to host 127.0.0.1:18754 left intact
   ```
   request.bin, both on the one connection:
   ```
   GET /a HTTP/1.1\r\nHost: 127.0.0.1:18754\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n
   GET /b HTTP/1.1\r\nHost: 127.0.0.1:18754\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n
   ```
2. Same settings over TLS (`-Tls`), `-k` in both groups: exit 0, `ok[1 0]` / `ok[0 0]`,
   `* Reusing existing https: connection with host 127.0.0.1`, both requests on one connection.
3. `-k` in the first group only: the second group opens a new connection -
   `* Hostname 127.0.0.1 was found in DNS cache`, `*   Trying 127.0.0.1:18754...`, a new
   handshake, `* schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - ...`, `* closing connection #1`,
   `curl: (60) ...`; stdout `ok[1 0]` / `[1 1]`; exit 60; request.bin holds only `GET /a`.

### Decisions (ADR-0285, decided by Claude under Stewart's delegation)

- One run-wide `ConnectionCache` (new, `Curl.Networking`) holds what `PoolingConnector` kept; each
  group's `PoolingConnector(inner, cache, configuration)` shares it, and the runner closes it once
  the run ends (`CurlCommandRunner`'s new `runConnectionCache`). A connector made with
  `(inner, timeProvider)` still owns its pool, so every other caller is unchanged.
- `ConnectionPoolKey.Configuration` carries the group's `OptionGroupConnectionSettings` (new,
  `Curl.Console`): both `TlsClientOptions` sets, `--connect-to`, Unix socket, local binding,
  `-4`/`-6`, PROXY line, pre-proxy and ALPN list. Default taken: include every connection-shaping
  setting the key did not already carry, since reusing a connection opened with different
  settings is wrong output, while an unneeded new connection only costs a handshake.
- Connection numbers (`#N`) now count across the run from the shared cache.

### Checked with our binary

The same run 1 against `Curl.Console`'s `curl.exe` printed `* Reusing existing http: connection
with host 127.0.0.1`, sent `GET /b` on the held connection and printed `ok[1 0]` / `ok[0 1]`: the
runner's `%{conn_id}` counts every transfer that connected instead of naming the reused
connection, within one group as across groups - filed as BL-1052. The DNS-cache line of run 3
is not printed because the DNS cache is still per group - filed as BL-1053.

### Touches

Added `Record-CurlExchange.ps1` (the measuring switch the criteria ask for) and
`Documentation/Planning/Decisions` (ADR-0285, its index line, ADR-0126's status line). No task
in Doing names either.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. A later --next option group reuses an earlier group's idle connection when their TLS, proxy and connection settings match; -v prints the reuse line and %{num_connects} is 0
