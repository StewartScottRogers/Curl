---
id: BL-5935
title: Pass the transfer's diagnostic log on the HTTP handler's ConnectTarget so --log-level shows dns, connect and tls lines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ConnectDiagnosticLog.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5935 — Pass the transfer's diagnostic log on the HTTP handler's ConnectTarget so --log-level shows dns, connect and tls lines

## Goal

With `--log-level`, an `http` or `https` transfer logs the connect's `dns`, `connect`, `proxy`, `tls` and `quic` lines (ADR-0222).

## Context

Found by BL-5886. `HttpProtocolHandler.TargetOf(HttpRequestPlan)` (`Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`) sets `PoolScheme` and `Events` on its `ConnectTarget` but never `DiagnosticLog = plan.Context.DiagnosticLog`, so `curl --log-level verbose https://localhost/` writes only `cli`, `runner`, `proxy` and `retry` lines, and warnings such as `CRL <uri> could not be fetched: ...` are lost. Other handlers (for example `S3Client`) set it. Check for other places the HTTP handler builds a target.

## Acceptance criteria

- [x] A unit test in `Curl.Original.Protocol.Http.UnitTests` shows the connector receives a target whose `DiagnosticLog` is the transfer context's, for a direct and a forward-proxy request.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `TargetOf(HttpRequestPlan)` is the one place the HTTP handler hands the connector a target
  (TCP, QUIC and both HTTP/3 races take it from there; `TargetOf(CurlUrl)` is used only to
  build it and to read `UseTls`), so setting `DiagnosticLog` there covers every connect.
- Tests: `HttpProtocolHandlerTests.ConnectDiagnosticLog.cs`; both failed before the fix.
- Checked the built binary: `curl --log-level verbose http://localhost:1/` now writes the
  `connect` and `dns` lines.
- `dotnet build`: 0 warnings, 0 errors. Fast tests green. `Measure-CodeQuality.ps1` on the
  library: 0 failing members.
- No new ADR: this carries out ADR-0222. No option changed, so `--ai-help` is unaffected.

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: df390bac 3d836d5e.
- 2026-11-25: Doing -> Done. --log-level now shows the dns, connect, proxy, tls and quic lines for http and https transfers
