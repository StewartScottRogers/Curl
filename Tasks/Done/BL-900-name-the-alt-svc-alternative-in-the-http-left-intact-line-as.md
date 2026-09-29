---
id: BL-900
title: Name the alt-svc alternative in the HTTP left intact line as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-623]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-900 — Name the alt-svc alternative in the HTTP left intact line as curl does

## Goal

A transfer that connected to an alt-svc alternative ends its `-v` output with
`* Connection #0 to host <althost>:<altport> left intact`, naming the alternative as curl 8.21.0
does, not the origin.

## Context

- Found in BL-623 (ADR-0214). Measured case 1 in BL-623's Notes: with the entry
  `h1 localhost 18499 h1 localhost 18443`, curl prints `* Connection #0 to host localhost:18443 left intact`;
  ours prints `localhost:18499`.
- `HttpProtocolHandler.LeftIntactLine` (`Curl.Protocol.Http.UnitLibrary`) passes `target.Host` and
  `target.Port`; when `target.AltSvcRoute` is set it should pass the alternative's host and port.
- Check with `Record-CurlExchange.ps1 -Tls` whether a `--connect-to` mapping also changes the host that
  line names, and pin whichever curl prints (a separate task if it differs from ours).

## Acceptance criteria

- [x] `Curl.Protocol.Http.UnitTests` pins the left-intact line naming the alternative for a request with
      `HttpRequestOptions.AltSvcRoute`, and the origin without one.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- `HttpProtocolHandler.LeftIntactLine` now names, in order: the Unix socket, the alt-svc alternative (`target.AltSvcRoute.Alternative`), else the target host and port. Pinned by `ExecuteAsync_KeptAliveWithAnAltSvcRoute_ReportsTheAlternativeLeftIntact` and `..._KeptAliveWithoutAnAltSvcRoute_ReportsTheOriginLeftIntact`.
- `--connect-to` measured (curl 8.21.0, `Record-CurlExchange.ps1 -Port 18499 -CurlArgs -v,--connect-to,example.invalid:80:127.0.0.1:18499,http://example.invalid/`): curl ends `* Connection #0 to host 127.0.0.1:18499 left intact`, naming the destination. Ours names the URL's host, and the mapping lives in `Curl.Networking.UnitLibrary`, outside this task's touches, so it is filed as BL-973.
- Measure-CodeQuality: `Curl.Protocol.Http.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The -v left intact line names the alt-svc alternative a transfer connected to, as curl 8.21.0 does
