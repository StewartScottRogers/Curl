---
id: BL-3413
title: Interpret upstream scripts/checksrc.pl in C# for the sample C file of case 1185
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1185Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1185.cs, Curl.Conformance.UnitTests/UpstreamTest1185ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed:
---
# BL-3413 — Interpret upstream scripts/checksrc.pl in C# for the sample C file of case 1185

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- 2026-10-23 (lane 6, first claim): requeued only because this run's cost cap ran low. The run spent it reading checksrc.pl, not on a missing prerequisite: no other task is needed. Measured and decided:
  - The runner matches the script by file name, so the key is `checksrc.pl` (`UpstreamCaseScreening.PerlScriptName`). Name the classes for what they interpret, not for the case: `UpstreamChecksrcScript` / `UpstreamChecksrcRun`, `UpstreamPerlCommandScripts.Checksrc.cs`, `UpstreamChecksrcScriptTests.cs`. Once those files exist, replace the 1185-named entries in `touches` with them. No Doing task on origin/work/dark-factory names them.
  - `%LOGDIR` is absolute (`UpstreamConformanceTests`, `logDirectory.FullName`), but the expected stdout is `./%LOGDIR/code1185.c:...`. checksrc.pl puts `$dir/` in front only when a path does not match `^\.?\.?/`, so a plain port would fail on Linux and macOS. Decision, to record in a new ADR (0806 or next): put `-D` dir in front of every path that does not start with `./` or `../` (absolute paths included), and open an absolute path as it is.
  - Strip CR LF or LF from every line on every platform (the script's Windows rule), so the result does not depend on the platform. COPYRIGHTYEAR never warns: it runs git, and the case's file is in no repository.
  - Uncommitted (the shift stashes it): `Curl.Conformance.UnitLibrary/UpstreamChecksrcScript.cs` (the entry point) and `UpstreamChecksrcRun.cs` (options, usage, checksrc.skip, .checksrc, `!checksrc!` instructions, checkwarn, the file and suppression counters). Fix first: it uses `[GeneratedRegex]`, which this library's CLAUDE.md forbids because generated code counts against coverage. Switch to `static readonly Regex` fields with `UpstreamRegex.MatchTimeout`, as `UpstreamTest1276Script` does.
  - Still to write: `ScanFile` and the line checks of checksrc.pl lines 532-1268 (curl-8_22_0), in the script's order. Perl quirks to keep: SPACEBEFOREPAREN's typedef test reads the keyword capture when `first` is `*`; `if($l)` is false for `"0"`; the banned-function loop replaces `prefix.bad.suff` with `prefix` plus `x` times (len+1); `goto comment` loops the comment stripping; and `#line` `next` skips `$line++` and the prev-line updates. Then write the tests to 100% line and branch coverage, the registration file, add `1185` to `PassingUpstreamCases.txt`, write the ADR, and run Measure-CodeQuality once for Curl.Conformance.UnitLibrary.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Backlog. Run's cost cap ran low after the partial port; no prerequisite missing, see Notes for what is done and left
