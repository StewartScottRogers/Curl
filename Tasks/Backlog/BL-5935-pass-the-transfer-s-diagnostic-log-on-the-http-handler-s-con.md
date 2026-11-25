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
completed:
---
# BL-5935 — Pass the transfer's diagnostic log on the HTTP handler's ConnectTarget so --log-level shows dns, connect and tls lines

## Goal

With `--log-level`, an `http` or `https` transfer logs the connect's `dns`, `connect`, `proxy`, `tls` and `quic` lines (ADR-0222).

## Context

Found by BL-5886. `HttpProtocolHandler.TargetOf(HttpRequestPlan)` (`Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`) sets `PoolScheme` and `Events` on its `ConnectTarget` but never `DiagnosticLog = plan.Context.DiagnosticLog`, so `curl --log-level verbose https://localhost/` writes only `cli`, `runner`, `proxy` and `retry` lines, and warnings such as `CRL <uri> could not be fetched: ...` are lost. Other handlers (for example `S3Client`) set it. Check for other places the HTTP handler builds a target.

## Acceptance criteria

- [ ] A unit test in `Curl.Original.Protocol.Http.UnitTests` shows the connector receives a target whose `DiagnosticLog` is the transfer context's, for a direct and a forward-proxy request.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
