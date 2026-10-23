---
id: BL-3416
title: Finish the checksrc.pl entry point and run options in C# (UpstreamChecksrcScript, UpstreamChecksrcRun)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406]
touches: [Curl.Conformance.UnitLibrary/UpstreamChecksrcScript.cs, Curl.Conformance.UnitLibrary/UpstreamChecksrcRun.cs, Curl.Conformance.UnitTests/UpstreamChecksrcRunTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3416 — Finish the checksrc.pl entry point and run options in C# (UpstreamChecksrcScript, UpstreamChecksrcRun)

## Goal

UpstreamChecksrcScript and UpstreamChecksrcRun interpret checksrc.pl's options, usage, skip files and instructions, and call a line-scanning step.

## Context

Upstream curl 8.22.0 (curl-8_22_0) scripts/checksrc.pl, run for the sample C file of upstream case 1185 by the conformance runner, which matches the script by file name (UpstreamCaseScreening.PerlScriptName). Library rules: Curl.Conformance.UnitLibrary/CLAUDE.md. Split from BL-3413; see its Notes (Deferred) for the measured decisions.

## Acceptance criteria

- [ ] UpstreamChecksrcScript (entry point) and UpstreamChecksrcRun exist in Curl.Conformance.UnitLibrary and handle the options, usage text, checksrc.skip, .checksrc, `!checksrc!` instructions, checkwarn and the file and suppression counters of checksrc.pl
- [ ] No `[GeneratedRegex]` is used: regexes are `static readonly Regex` fields with `UpstreamRegex.MatchTimeout`, as UpstreamTest1276Script does
- [ ] A -D dir is put in front of every path that does not start with `./` or `../` (absolute paths included) and an absolute path is opened as it is; CR LF or LF is stripped from every line on every platform
- [ ] Curl.Conformance.UnitTests/UpstreamChecksrcRunTests.cs covers the new code to 100% line and branch coverage, and `dotnet build` and the fast tests pass

## Notes

- Split from BL-3413 (over its cost cap).
- Continues the stashed work of BL-3413.
- The stash holds a draft of both files (fix its `[GeneratedRegex]` use first). The per-line checks (ScanFile) are BL-3417 and BL-3418; define the seam they plug into here (one interface-free method per check group, called in the script's order) and leave it calling nothing until they land. COPYRIGHTYEAR never warns: it runs git and the case's file is in no repository.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 63150620 8130eb23 3550c5ef 78e0a7b4.
