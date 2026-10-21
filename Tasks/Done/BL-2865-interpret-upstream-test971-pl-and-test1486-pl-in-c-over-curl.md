---
id: BL-2865
title: Interpret upstream test971.pl and test1486.pl in C# over Curl's option and write-out tables and put cases 971 and 1486 on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2863, BL-2864]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest971Script.cs, Curl.Conformance.UnitLibrary/UpstreamTest1486Script.cs, Curl.Conformance.UnitTests/UpstreamTest971ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamTest1486ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2865 — Interpret upstream test971.pl and test1486.pl in C# over Curl's option and write-out tables and put cases 971 and 1486 on the ratchet

## Goal

Upstream cases 971 ("options-in-versions and docs/cmdline-opts are in sync") and 1486 ("write-out.md and tool_writeout.c are in sync") end `Passed` with `$env:CURL_UPSTREAM_CASE` on Windows, through C# interpretations of test971.pl and test1486.pl over the vendored docs and Curl's option table (`CommandLineOptionTable`, `CurlOptionAliasTable`) and write-out variable table, and are on `PassingUpstreamCases.txt`.

## Context

Split from BL-2850. Needs the dispatch seam (BL-2863) and the vendored docs (BL-2864). Read both scripts at tag `curl-8_22_0`. Where the check finds Curl's table differing from upstream's docs, file a task to fix Curl (in `Curl.Cli.UnitLibrary`, with a focused unit test in `Curl.Cli.UnitTests`, ADR-0482, keeping `--ai-help` right) rather than widening this task. Never run Perl.

## Acceptance criteria

- [x] Cases 971 and 1486 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [x] Each interpretation has its own unit tests (a matching and a differing input).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-21 (lane 2, first claim): the whole change is written and green, left uncommitted for the shift's stash because it needs `UpstreamConformanceTests.cs`, which BL-2841 (in Doing) also touches (rule 3: added to `touches`, requeued). Next claim: apply the stash, check `git status` shows the files below, run `dotnet build Curl.slnx -warnaserror` and the fast tests once, tick the criteria, commit, Done.
  - `UpstreamTest971Script` / `UpstreamTest1486Script` (Curl.Conformance.UnitLibrary): faithful C# readings of test971.pl (+ allversions.pm) and test1486.pl at `curl-8_22_0`, hand-parsed (no `Regex`, per the library's CLAUDE.md), each with a `Check` and a `RunAsync` of the `perlCommandScripts` delegate shape. Tests: `UpstreamTest971ScriptTests`, `UpstreamTest1486ScriptTests` (matching and differing inputs).
  - `UpstreamConformanceTests.cs`: a `PerlCommandScripts` dictionary registering both, passed as `perlCommandScripts:` to the ratchet's runner. `PassingUpstreamCases.txt`: 971 and 1486 added.
  - `Curl.Conformance.UnitTests.csproj` (added to touches; no Doing task names it): the vendored `docs/` and `src/` are copied a second time beside `UpstreamTestData` (`ContentWithTargetPath`), because the cases name `%SRCDIR/../docs` and `%SRCDIR` is `UpstreamTestData`; the copy inside `UpstreamTestData` stays for `UpstreamTestDataTests` (BL-2829's file).
  - Decision: the interpretations read the vendored upstream files the command names, as the scripts do, not Curl's own option and write-out tables; the cases check upstream's docs against upstream's source, and comparing Curl's tables is the gap office's options and write-out measurement. So no Curl difference was found and no Cli task filed.
  - Measured: cases 971 and 1486 `Passed` with `CURL_UPSTREAM_CASE`; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10; build -warnaserror clean; fast tests green.
- 2026-10-21 (lane 3, second claim): the stash did not apply because BL-2887 had meanwhile added a `PerlCommandScripts` dictionary (with test1707.pl) to `UpstreamConformanceTests.cs` and passed it to the runner; the four new files were restored from the stash and 971 and 1486 added as two entries of that dictionary. Re-measured on this branch: build -warnaserror clean, cases 971 and 1486 `Passed`, `Curl.Conformance.UnitTests` fast tests 3899 passed / 0 failed, `Curl.Conformance.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: a61dc661 969fe749 309149d6.
- 2026-10-21: Doing -> Backlog. Needs Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, which BL-2841 (Doing) also touches; work is complete in the stash, next claim commits it
- 2026-10-21: Backlog -> Doing. Criteria at claim: a61dc661 969fe749 309149d6.
- 2026-10-21: Doing -> Done. Cases 971 and 1486 pass through C# readings of test971.pl and test1486.pl and are on the ratchet
