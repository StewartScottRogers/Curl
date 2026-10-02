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
completed:
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

- [ ] Measured first: the Unix socket case, stderr in Notes.
- [ ] `Curl.Console.UnitTests` pin the proxy, negative-cache and resolved-then-refused lines as measured in BL-1157 Notes, and the Unix socket's as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
