---
id: BL-481
title: Print curl's 'Hostname H was found in DNS cache' line before a second connect to a resolved host
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-481 — Print curl's 'Hostname H was found in DNS cache' line before a second connect to a resolved host

## Goal

When a command line opens a fresh connection to a host an earlier transfer on it already resolved, Curl's `-v` prints curl 8.21.0's `* Hostname H was found in DNS cache` line just before `*   Trying`, as curl does.

## Context

- Found in BL-477 (ADR-0112). Measured 2026-09-27 with curl 8.21.0 (mingw Schannel), `Record-CurlExchange.ps1`, `-s -v http://127.0.0.1:P/a http://127.0.0.1:P/b`:
  - each answered `HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nhi`: after `shutting down connection #0`, curl prints `Hostname 127.0.0.1 was found in DNS cache` then `Trying 127.0.0.1:P...` for `/b`;
  - after `Connection 0 seems to be dead` / `shutting down connection #0` (HTTP/1.0 keep-alive run to close) and after `Issue another request to this URL` (a reused connection that died), the same line precedes the fresh `Trying`.
- The host here is an IP literal, and curl still prints the line, so it is not tied to a DNS lookup having happened; measure a name (`localhost`), `--resolve` and `--connect-to` before pinning which transfers print it.
- `TcpConnector` in `Curl.Networking.UnitLibrary` prints `Trying`; nothing in the solution prints this line today.

## Acceptance criteria

- [x] A `TcpConnector` test pins that a second connect to a host already connected to on the same command line reports `Hostname H was found in DNS cache` immediately before its `Trying` line, and the first connect does not.
- [x] The cases measured for a host name, `--resolve` and `--connect-to` are recorded in this task's Notes and each has a test pinning curl's answer.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

- Filed from BL-477 (2026-09-27).
- Measured 2026-09-27, curl 8.21.0 (mingw Schannel), `Record-CurlExchange.ps1 -Connections 2`, `-s -v`, each response `HTTP/1.1 200 OK` + `Connection: close` + `Content-Length: 2`. The key is the host and port being resolved:
  - `localhost` twice: `Hostname localhost was found in DNS cache` before the second transfer's `Host localhost:P was resolved.` / `IPv6` / `IPv4` / `Trying` lines (Curl prints none of those three lines yet: BL-482).
  - `--resolve foo.example:P:127.0.0.1`, two URLs: `Added foo.example:P:127.0.0.1 to DNS cache` then `Hostname foo.example was found in DNS cache` on both transfers, the first included.
  - `--connect-to foo.example:P:127.0.0.1:P` after `http://127.0.0.1:P/`: the line names `127.0.0.1`, the mapped host; `--connect-to foo.example:P:localhost:P` names `localhost`. `--connect-to 127.0.0.1:P2:127.0.0.1:P` with `/b` on P2 prints it too: the key is the mapped port.
  - Another host (`127.0.0.1` then `localhost`) or another port: no line. Two refused dials to `127.0.0.1:1`: line before the second (a failed dial stays cached). `nonexistent.invalid` twice: no line (a failed lookup is not cached). `-x http://127.0.0.1:P`, two URLs: the line names the proxy host.
- Tests: `TcpConnectorTests.DnsCache.cs`, one per case above (proxy pinned on a refused tunnel dial, which needs no CONNECT script).
- Decision (ADR-0113): the cache lives in `TcpConnector`, keyed host:port ignoring host case, holding the addresses; a cached key is dialled from the cache without the resolver, as curl does. SOCKS local resolution goes through it too (not measured; same `Curl_resolv` in curl).
- `Documentation/Planning/Decisions` added to `touches` for ADR-0113 and its README row; no task in Doing names it.
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members. Its first run exited 1 with every test project reporting Passed; a rerun was clean, so that was a flaky test outside this change.
- Follow-up filed: BL-482 (the `Host H:P was resolved.`, `IPv6`/`IPv4` and `Added ... to DNS cache` lines).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v prints curl's 'Hostname H was found in DNS cache' line for a host:port resolved earlier on the command line or answered by --resolve
