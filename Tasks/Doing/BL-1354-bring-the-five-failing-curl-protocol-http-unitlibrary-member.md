---
id: BL-1354
title: Bring the five failing Curl.Protocol.Http.UnitLibrary members back under the quality gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1354 — Bring the five failing Curl.Protocol.Http.UnitLibrary members back under the quality gates

## Goal

`powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Context

Measured 2026-10-03 while finishing BL-1331 (none of these were changed by it); five members fail:

- `HttpProtocolHandler.SettleConnection` (`HttpProtocolHandler.cs:805`): complexity 14, branch 85.71%.
- `HttpProtocolHandler.ExchangeAsync` (`HttpProtocolHandler.cs:969`): complexity 12.
- `HttpResponseHeadReader..ctor(IConnection)` (`HttpResponseHeadReader.cs:25`): complexity 12.
- `HttpRequestBodyWriter.ReportStreamRead` (`HttpRequestBodyWriter.cs:329`): complexity 12.
- `Http3StreamConnection..ctor` (`Http3StreamConnection.cs:46`): branch 50%.

Split methods to at most 10 and cover the missing branches with tests; never raise a threshold in `CodeMetricsConfig.txt`.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.
- [ ] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
