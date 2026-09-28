---
id: BL-481
title: Print curl's 'Hostname H was found in DNS cache' line before a second connect to a resolved host
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
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

- [ ] A `TcpConnector` test pins that a second connect to a host already connected to on the same command line reports `Hostname H was found in DNS cache` immediately before its `Trying` line, and the first connect does not.
- [ ] The cases measured for a host name, `--resolve` and `--connect-to` are recorded in this task's Notes and each has a test pinning curl's answer.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

- Filed from BL-477 (2026-09-27).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
