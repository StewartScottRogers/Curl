---
id: BL-2067
title: Bring TcpConnector.AnswerProxyChallengeAsync under the complexity gate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2067 — Bring TcpConnector.AnswerProxyChallengeAsync under the complexity gate

## Goal

`Measure-CodeQuality.ps1` reports every member of Curl.Networking.UnitLibrary inside the gates, so the library shows `pass` on the coverage page.

## Context

The coverage page (<https://stewartscottrogers.github.io/Curl/coverage/>, measured 2026-10-11 from commit 53f17a0) lists `TcpConnector.AnswerProxyChallengeAsync` (`Curl.Networking.UnitLibrary/TcpConnector.cs:1753`) at cyclomatic complexity 12 and CRAP 12; the gate is 10. The build's `CA1502` passes it, because the page uses the coverage report's complexity, which counts the compiled method (async state machine branches included). Split the method into smaller, well-named methods that each stay at 10 or below by that measure; behaviour does not change.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no member outside a gate, at 100% line and branch coverage.
- [ ] No existing test was changed or removed to get there; any new member has its own tests.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
