---
id: BL-2065
title: Bring UpstreamCaseRunner.Arguments and RunScreenedAsync's lambda under the complexity gate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2065 — Bring UpstreamCaseRunner.Arguments and RunScreenedAsync's lambda under the complexity gate

## Goal

`Measure-CodeQuality.ps1` reports every member of Curl.Conformance.UnitLibrary inside the gates, so the library shows `pass` on the coverage page.

## Context

The coverage page (<https://stewartscottrogers.github.io/Curl/coverage/>, measured 2026-10-11 from commit 53f17a0) lists these over the complexity gate of 10:

- `UpstreamCaseRunner.Arguments` (`Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs:421`): complexity 12.
- The lambda inside `UpstreamCaseRunner.RunScreenedAsync` (`UpstreamCaseRunner.cs:303`): 12.

The build's `CA1502` passes them, because the page uses the coverage report's complexity, which counts the compiled method. Split each into smaller, well-named methods that stay at 10 or below by that measure; for the lambda, move its body into a named method. Behaviour does not change.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports no member outside a gate, at 100% line and branch coverage.
- [x] No existing test was changed or removed to get there; any new member has its own tests.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. Split Arguments and the tunnel lambda under the gate; covered FtpControlChannelResponder.Answer's full-text REPLY branch. Measure-CodeQuality: 0 failing, 100/100.
