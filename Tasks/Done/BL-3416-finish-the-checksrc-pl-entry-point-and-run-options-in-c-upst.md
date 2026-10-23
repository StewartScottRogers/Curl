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
completed: 2026-10-23
---
# BL-3416 — Finish the checksrc.pl entry point and run options in C# (UpstreamChecksrcScript, UpstreamChecksrcRun)

## Goal

UpstreamChecksrcScript and UpstreamChecksrcRun interpret checksrc.pl's options, usage, skip files and instructions, and call a line-scanning step.

## Context

Upstream curl 8.22.0 (curl-8_22_0) scripts/checksrc.pl, run for the sample C file of upstream case 1185 by the conformance runner, which matches the script by file name (UpstreamCaseScreening.PerlScriptName). Library rules: Curl.Conformance.UnitLibrary/CLAUDE.md. Split from BL-3413; see its Notes (Deferred) for the measured decisions.

## Acceptance criteria

- [x] UpstreamChecksrcScript (entry point) and UpstreamChecksrcRun exist in Curl.Conformance.UnitLibrary and handle the options, usage text, checksrc.skip, .checksrc, `!checksrc!` instructions, checkwarn and the file and suppression counters of checksrc.pl
- [x] No `[GeneratedRegex]` is used: regexes are `static readonly Regex` fields with `UpstreamRegex.MatchTimeout`, as UpstreamTest1276Script does
- [x] A -D dir is put in front of every path that does not start with `./` or `../` (absolute paths included) and an absolute path is opened as it is; CR LF or LF is stripped from every line on every platform
- [x] Curl.Conformance.UnitTests/UpstreamChecksrcRunTests.cs covers the new code to 100% line and branch coverage, and `dotnet build` and the fast tests pass

## Notes

- Split from BL-3413 (over its cost cap).
- Continues the stashed work of BL-3413.
- The stash holds a draft of both files (fix its `[GeneratedRegex]` use first). The per-line checks (ScanFile) are BL-3417 and BL-3418; define the seam they plug into here (one interface-free method per check group, called in the script's order) and leave it calling nothing until they land. COPYRIGHTYEAR never warns: it runs git and the case's file is in no repository.
- 2026-10-23 (lane 3): finished. The seam is `UpstreamChecksrcRun.CheckLineFirstHalf` (BL-3417) and `CheckLineSecondHalf` (BL-3418), static and empty, each taking the file's `UpstreamChecksrcRun.FileScan` (`$file`, `$line`, `$l` as `Text`, `$ol` as `Original`, `$column`, `$prevl`, `$prevpl`, `$prep`, `$prevp`, `$prevfreeindent`, `$prevfreevar`, `$incomment`, `@copyright`, `%includes`); the checks call `Warn`, `MaxColumn`, `Indent`, `IsBanned`, `KnownFunctions` and `IsEnabled`. Make them instance methods if a check needs the run. The frame already handles `#line` (renames the file, renumbers, skips the checks and the line counter) and the end-of-line updates (`FileScan.EndLine`), and `CheckEndOfFile` does COPYRIGHT, OPENCOMMENT and the unused-ignore warnings, so BL-3417 and BL-3418 need not.
- Decided: a non-numeric true disable scope (`disable FOO abc`) counts from -1, as Perl's decrement of a string makes it, so it never reaches zero; `%ignore_set`'s keys are walked in insertion order (Perl's hash order is random). Platform-neutral: tests write files in a temp folder with forward-slash paths and pin no OS text, so CI on Linux and macOS holds with the fast tests green here.
- Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members after splitting `Run` and `ReadLocalFile`.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 63150620 8130eb23 3550c5ef 78e0a7b4.
- 2026-10-23: Doing -> Done. checksrc.pl entry point, options, skip files, instructions, checkwarn and scanfile frame with the per-line seam; 16 tests, quality gates clean
