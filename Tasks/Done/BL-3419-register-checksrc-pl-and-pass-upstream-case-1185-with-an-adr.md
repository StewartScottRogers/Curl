---
id: BL-3419
title: Register checksrc.pl and pass upstream case 1185 with an ADR
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3416, BL-3417, BL-3418]
touches: [Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Checksrc.cs, Curl.Conformance.UnitLibrary/UpstreamChecksrcScript.cs, Curl.Conformance.UnitTests/UpstreamChecksrcScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3419 — Register checksrc.pl and pass upstream case 1185 with an ADR

## Goal

Case 1185 passes in the upstream conformance runner through the C# checksrc.pl interpreter, with the decisions recorded in an ADR.

## Context

Upstream curl 8.22.0 (curl-8_22_0) scripts/checksrc.pl, run for the sample C file of upstream case 1185 by the conformance runner, which matches the script by file name (UpstreamCaseScreening.PerlScriptName). Library rules: Curl.Conformance.UnitLibrary/CLAUDE.md. Split from BL-3413; see its Notes (Deferred) for the measured decisions.

## Acceptance criteria

- [x] UpstreamPerlCommandScripts.Checksrc.cs registers UpstreamChecksrcScript under the key `checksrc.pl`
- [x] `1185` is in Curl.Conformance.UnitTests/PassingUpstreamCases.txt and the upstream case test for 1185 passes on Windows with the expected stdout `./%LOGDIR/code1185.c:...`
- [x] UpstreamChecksrcScriptTests.cs covers the entry point end to end
- [x] A new ADR (next free number) records the -D dir prefix rule, the line ending rule and that COPYRIGHTYEAR never warns, marked "Decided by Claude under Stewart's delegation"
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` run once for Curl.Conformance.UnitLibrary shows no member outside a gate; `dotnet build` and the fast tests pass

## Notes

- Split from BL-3413 (over its cost cap).
- Needs BL-3416, BL-3417 and BL-3418 done.
- 2026-10-23 (lane 3): added `Curl.Conformance.UnitLibrary/UpstreamChecksrcScript.cs` to touches only to correct its remarks, which cited ADR-0806 (taken by the PIPEWAIT ADR) for the -D rule; it now cites ADR-0807. No Doing task on origin/work/dark-factory names it.
- ADR-0807 records the -D directory rule, the CR LF/LF rule and COPYRIGHTYEAR. %LOGDIR is absolute, so the report reads `./` + %LOGDIR on every platform; the case output does not depend on the platform (CI on Linux and macOS: met by keeping it platform-neutral, fast tests green here).
- Measured: test1185 passes listed; Curl.Conformance.UnitTests 4292 passed, 0 failed; whole fast suite 0 failed; Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 34e2419b 19083d3c db4153fe 78026c72 f3e1a8c0.
- 2026-10-23: Doing -> Done. checksrc.pl is registered and upstream case 1185 passes on the ratchet, decisions in ADR-0807
