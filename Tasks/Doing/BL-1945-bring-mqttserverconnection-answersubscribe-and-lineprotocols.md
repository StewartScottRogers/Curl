---
id: BL-1945
title: Bring MqttServerConnection.AnswerSubscribe and LineProtocolServerCommands.PerlDoubleQuoted under the complexity and branch gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1945 — Bring MqttServerConnection.AnswerSubscribe and LineProtocolServerCommands.PerlDoubleQuoted under the complexity and branch gates

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports no failing member.

## Context

Measured by BL-1944 on 2026-10-09 (Curl.Conformance.UnitLibrary: 100% lines, 99.95% branches, 2 failing members): `MqttServerConnection.AnswerSubscribe(byte[])` (MqttServerConnection.cs:259) has cyclomatic complexity 16 and 93.75% branch coverage; `LineProtocolServerCommands.PerlDoubleQuoted(string)` (LineProtocolServerCommands.cs:93) has complexity 12. The gate is complexity at most 10 and 100% branch coverage (root CLAUDE.md, "Quality gates"). Split each into smaller private methods, and add the test that reaches the uncovered AnswerSubscribe branch; behaviour must not change.

## Acceptance criteria

- [ ] Both methods (or what they are split into) have complexity of at most 10 and 100% line and branch coverage.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no case on PassingUpstreamCases.txt stops passing.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-10: Backlog -> Doing.
