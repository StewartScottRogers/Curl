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
completed:
---
# BL-900 — Name the alt-svc alternative in the HTTP left intact line as curl does

## Goal

A transfer that connected to an alt-svc alternative ends its `-v` output with
`* Connection #0 to host <althost>:<altport> left intact`, naming the alternative as curl 8.21.0
does, not the origin.

## Context

- Found in BL-623 (ADR-0213). Measured case 1 in BL-623's Notes: with the entry
  `h1 localhost 18499 h1 localhost 18443`, curl prints `* Connection #0 to host localhost:18443 left intact`;
  ours prints `localhost:18499`.
- `HttpProtocolHandler.LeftIntactLine` (`Curl.Protocol.Http.UnitLibrary`) passes `target.Host` and
  `target.Port`; when `target.AltSvcRoute` is set it should pass the alternative's host and port.
- Check with `Record-CurlExchange.ps1 -Tls` whether a `--connect-to` mapping also changes the host that
  line names, and pin whichever curl prints (a separate task if it differs from ours).

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pins the left-intact line naming the alternative for a request with
      `HttpRequestOptions.AltSvcRoute`, and the origin without one.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
