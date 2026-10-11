---
id: BL-2062
title: Bring HttpProtocolHandler.ExchangeAsync, ReportAuthRetryRewind and HttpExchange.Report under the complexity gate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2061]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2062 — Bring HttpProtocolHandler.ExchangeAsync, ReportAuthRetryRewind and HttpExchange.Report under the complexity gate

## Goal

`Measure-CodeQuality.ps1` reports every member of Curl.Protocol.Http.UnitLibrary inside the gates, so the library shows `pass` on the coverage page.

## Context

The coverage page (<https://stewartscottrogers.github.io/Curl/coverage/>, measured 2026-10-11 from commit 53f17a0) lists these over the complexity gate of 10:

- `HttpProtocolHandler.ExchangeAsync` (`Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1093`): complexity 14.
- `HttpProtocolHandler.ReportAuthRetryRewind` (`HttpProtocolHandler.cs:1617`): 12.
- `HttpExchange.Report` (`HttpProtocolHandler.cs:2729`): 12.

The build's `CA1502` passes them, because the page uses the coverage report's complexity, which counts the compiled method (async state machine branches included). Split each into smaller, well-named methods that stay at 10 or below by that measure; behaviour does not change. It runs after BL-2061, which fixes the library's other three members, so that the library passes once both are done.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no member outside a gate, at 100% line and branch coverage.
- [ ] No existing test was changed or removed to get there; any new member has its own tests.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
