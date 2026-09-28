---
id: BL-477
title: Report a pooled connection the server closed as dead before reusing it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-477 — Report a pooled connection the server closed as dead before reusing it

## Goal

When a second URL on the command line would reuse a pooled connection the server has already closed, Curl prints curl 8.21.0's `* Connection N seems to be dead` and `* shutting down connection #N` lines, then opens a fresh connection, as curl does.

## Context

- Found in BL-471 (ADR-0109). curl 8.21.0 (mingw Schannel) with `-s -v http://127.0.0.1:P/a http://127.0.0.1:P/b`, each answered `HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi` and closed: after `Connection #0 to host 127.0.0.1:P left intact` for `/a`, curl prints `* Connection 0 seems to be dead`, `* shutting down connection #0`, `* Hostname 127.0.0.1 was found in DNS cache`, then `Trying` and connection #1 for `/b`.
- Today `HttpProtocolHandler` reports `left intact` for that response but does not mark the connection reusable (ADR-0109), so `/b` opens connection #1 without the two dead-connection lines.
- `PoolingConnector` has no liveness check before reuse. Matching curl needs one (a zero-byte, non-blocking peek or equivalent through `IConnection`), and the handler then marking such a connection reusable. Measure again before pinning, including the DNS cache line.

## Acceptance criteria

- [x] A `PoolingConnector` test pins that a pooled connection found closed is reported with `Connection N seems to be dead` and `shutting down connection #N` and is not handed out.
- [x] A test over scripted connections pins the two-URL case above end to end in the order curl prints it.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

- Filed from BL-471 (2026-09-27).
- Measured 2026-09-27, curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -s -v http://127.0.0.1:P/a http://127.0.0.1:P/b`, server closing after each response:
  - `HTTP/1.0 200 OK`, `Connection: keep-alive`, body `hi`: after `Connection #0 ... left intact`, `Connection 0 seems to be dead`, `shutting down connection #0`, `Hostname 127.0.0.1 was found in DNS cache`, `Trying`, connection #1 (as filed).
  - `HTTP/1.1 200 OK`, `Content-Length: 2`: curl reuses #0 and prints `Recv failure: Connection was reset` and `Connection died, retrying a fresh connect` - no "seems to be dead". So curl calls a connection dead only when it already read the server's close; a zero-byte socket peek would be wrong.
  - `HTTP/1.1`, `Connection: close`: `Hostname 127.0.0.1 was found in DNS cache` precedes the second `Trying` too, so that line belongs to every fresh connect to a resolved host, not to this task: filed as BL-481.
- Decision (ADR-0112, decided by Claude under Stewart's delegation): `PooledConnection` notes a read into a non-empty buffer that returned zero (`PoolEntry.HasReadPeerClose`); `PoolingConnector` reports such an idle connection dead, closes it and tries the next one; the HTTP handler marks every connection it reports `left intact` reusable. No change to `IConnection`, so `Curl.Protocol.Abstractions.UnitLibrary` stays untouched.
- `Documentation/Planning/Decisions` added to `touches` for ADR-0112, its index line and ADR-0109's status; no task in Doing names it (BL-440 touches only `Curl.Console`).
- `Curl.Protocol.Http.UnitTests` now references `Curl.Networking.UnitLibrary`, so the end-to-end test runs the real `PoolingConnector` under the handler over scripted connections (`ExecuteAsync_SecondUrlAfterAnHttp10KeepAliveBodyRanToTheClose_ReportsThePooledConnectionDeadAndOpensConnection1`). Pool tests: `ConnectAsync_AfterAReadFoundTheServersClose_ReportsTheConnectionDeadAndOpensANewOne` and two neighbours.
- Gates 2026-09-27: `dotnet build -warnaserror` clean; fast tests green (Http 1140, Networking 803 + 6 skipped); `Measure-CodeQuality.ps1` 100% line and branch, 0 failing, worst CRAP 10 for both libraries.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A pooled connection whose close was read is reported 'Connection N seems to be dead' and 'shutting down connection #N' before a fresh connect, as curl 8.21.0 does
