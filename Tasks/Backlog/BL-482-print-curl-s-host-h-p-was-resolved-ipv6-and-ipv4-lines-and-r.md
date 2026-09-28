---
id: BL-482
title: Print curl's 'Host H:P was resolved.', IPv6 and IPv4 lines and --resolve's 'Added ... to DNS cache' line
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-482 — Print curl's 'Host H:P was resolved.', IPv6 and IPv4 lines and --resolve's 'Added ... to DNS cache' line

## Goal

With `-v`, a connect to a host name prints curl 8.21.0's `Host H:P was resolved.`, `IPv6: ...` and `IPv4: ...` lines before `Trying`, and each `--resolve` entry prints `Added H:P:A to DNS cache`, as curl does.

## Context

- Found in BL-481 (ADR-0113). Measured 2026-09-27 with curl 8.21.0 (mingw Schannel), `Record-CurlExchange.ps1`, `-s -v`:
  - `http://localhost:P/a` prints `Host localhost:P was resolved.`, `IPv6: ::1`, `IPv4: 127.0.0.1`, then `Trying [::1]:P...`; the second URL prints `Hostname localhost was found in DNS cache` then the same three lines again.
  - `--resolve foo.example:P:127.0.0.1` prints `Added foo.example:P:127.0.0.1 to DNS cache` and `Hostname foo.example was found in DNS cache`, then `Host foo.example:P was resolved.`, `IPv6: (none)`, `IPv4: 127.0.0.1`, for each transfer.
  - An IP literal (`http://127.0.0.1:P/`) prints none of the three lines.
- `TcpConnector.ResolveAsync` in `Curl.Networking.UnitLibrary` is where the cache line is reported today; where `Added` belongs (per transfer, as measured, or once) needs measuring with one URL and two.

## Acceptance criteria

- [ ] A `TcpConnector` test pins the three resolved lines for a host name, first and second connect, and none for an IP literal.
- [ ] A test pins `Added H:P:A to DNS cache` for a `--resolve` entry in curl's position.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

- Filed from BL-481 (2026-09-27).

## Log

- 2026-09-27: Created.
