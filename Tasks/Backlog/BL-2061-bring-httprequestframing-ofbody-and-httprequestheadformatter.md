---
id: BL-2061
title: Bring HttpRequestFraming.OfBody and HttpRequestHeadFormatter's FormContentTypeOf and Format under the complexity gate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2061 — Bring HttpRequestFraming.OfBody and HttpRequestHeadFormatter's FormContentTypeOf and Format under the complexity gate

## Goal

`Measure-CodeQuality.ps1` reports `HttpRequestFraming.OfBody`, `HttpRequestHeadFormatter.FormContentTypeOf` and `HttpRequestHeadFormatter.Format` inside the gates.

## Context

The coverage page (<https://stewartscottrogers.github.io/Curl/coverage/>, measured 2026-10-11 from commit 53f17a0) lists these over the complexity gate of 10:

- `HttpRequestFraming.OfBody` (`Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:292`): complexity 18.
- `HttpRequestHeadFormatter.FormContentTypeOf` (`HttpRequestHeadFormatter.cs:403`): 14.
- `HttpRequestHeadFormatter.Format` (`HttpRequestHeadFormatter.cs:119`): 12.

The build's `CA1502` passes them, because the page uses the coverage report's complexity, which counts the compiled method. Split each into smaller, well-named methods that stay at 10 or below by that measure; behaviour does not change. BL-2062 covers the other three Curl.Protocol.Http.UnitLibrary members on the page.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` lists none of the three members above, at 100% line and branch coverage.
- [ ] No existing test was changed or removed to get there; any new member has its own tests.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
