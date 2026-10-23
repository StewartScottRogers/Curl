---
id: BL-3419
title: Register checksrc.pl and pass upstream case 1185 with an ADR
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3416, BL-3417, BL-3418]
touches: [Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Checksrc.cs, Curl.Conformance.UnitTests/UpstreamChecksrcScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation]
requirement: none
created: 2026-10-23
completed:
---
# BL-3419 — Register checksrc.pl and pass upstream case 1185 with an ADR

## Goal

Case 1185 passes in the upstream conformance runner through the C# checksrc.pl interpreter, with the decisions recorded in an ADR.

## Context

Upstream curl 8.22.0 (curl-8_22_0) scripts/checksrc.pl, run for the sample C file of upstream case 1185 by the conformance runner, which matches the script by file name (UpstreamCaseScreening.PerlScriptName). Library rules: Curl.Conformance.UnitLibrary/CLAUDE.md. Split from BL-3413; see its Notes (Deferred) for the measured decisions.

## Acceptance criteria

- [ ] UpstreamPerlCommandScripts.Checksrc.cs registers UpstreamChecksrcScript under the key `checksrc.pl`
- [ ] `1185` is in Curl.Conformance.UnitTests/PassingUpstreamCases.txt and the upstream case test for 1185 passes on Windows with the expected stdout `./%LOGDIR/code1185.c:...`
- [ ] UpstreamChecksrcScriptTests.cs covers the entry point end to end
- [ ] A new ADR (next free number) records the -D dir prefix rule, the line ending rule and that COPYRIGHTYEAR never warns, marked "Decided by Claude under Stewart's delegation"
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` run once for Curl.Conformance.UnitLibrary shows no member outside a gate; `dotnet build` and the fast tests pass

## Notes

- Split from BL-3413 (over its cost cap).
- Needs BL-3416, BL-3417 and BL-3418 done.

## Log

- 2026-10-23: Created.
