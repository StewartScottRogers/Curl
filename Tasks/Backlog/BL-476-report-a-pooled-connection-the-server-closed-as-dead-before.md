---
id: BL-476
title: Report a pooled connection the server closed as dead before reusing it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-476 — Report a pooled connection the server closed as dead before reusing it

## Goal

When a second URL on the command line would reuse a pooled connection the server has already closed, Curl prints curl 8.21.0's `* Connection N seems to be dead` and `* shutting down connection #N` lines, then opens a fresh connection, as curl does.

## Context

- Found in BL-471 (ADR-0109). curl 8.21.0 (mingw Schannel) with `-s -v http://127.0.0.1:P/a http://127.0.0.1:P/b`, each answered `HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi` and closed: after `Connection #0 to host 127.0.0.1:P left intact` for `/a`, curl prints `* Connection 0 seems to be dead`, `* shutting down connection #0`, `* Hostname 127.0.0.1 was found in DNS cache`, then `Trying` and connection #1 for `/b`.
- Today `HttpProtocolHandler` reports `left intact` for that response but does not mark the connection reusable (ADR-0109), so `/b` opens connection #1 without the two dead-connection lines.
- `PoolingConnector` has no liveness check before reuse. Matching curl needs one (a zero-byte, non-blocking peek or equivalent through `IConnection`), and the handler then marking such a connection reusable. Measure again before pinning, including the DNS cache line.

## Acceptance criteria

- [ ] A `PoolingConnector` test pins that a pooled connection found closed is reported with `Connection N seems to be dead` and `shutting down connection #N` and is not handed out.
- [ ] A test over scripted connections pins the two-URL case above end to end in the order curl prints it.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

- Filed from BL-471 (2026-09-27).

## Log

- 2026-09-27: Created.
