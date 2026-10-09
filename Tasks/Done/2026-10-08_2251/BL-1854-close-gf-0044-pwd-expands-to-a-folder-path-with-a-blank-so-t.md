---
id: BL-1854
title: Close GF-0044: %PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1854 — Close GF-0044: %PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0044 (%PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL), so a later gap analysis measures each of `behaviour:test3009` as `match`.

## Context

- Finding: GF-0044, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3009`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measured: test3009 expected 'upstream test3009 passes', actual '<verify><protocol> differs at byte 94 (line 6): expected the end, got "GET /AppData/Local/Curl/gap/upstream/8.21.0/tests/not-there HTTP/1.1\r\n"'. The command is -O --output-dir %PWD/not-there, which upstream expects to fail with exit 23 after one request. %PWD is the release's tests folder under C:/Users/Stewart Rogers/AppData/Local, so 'Rogers/AppData/...' became a second URL. A copy of the case under a tests folder with no blank (Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/tests/data), rerun on 6383c570, passed. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 3009

Suggestion, copied from the finding:

Curl's behaviour needs no change: it passes test3009 once %PWD holds no blank. Make Curl.Conformance.UnitLibrary's UpstreamCaseRunner own %PWD: take it as a parameter and refuse one that holds a blank, as it already does for %LOGDIR. Pin the refusal in Curl.Conformance.UnitTests. The office's Measure-UpstreamCases.cs must then pass a tests folder with no blank, for example by copying the release under the run folder. That part is office work, filed interactively.

## Acceptance criteria

- [x] `behaviour:test3009`: Curl answers what curl 8.21.0 answers, `upstream test3009 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Curl's own behaviour needed no change: the finding's rerun of test3009 from a tests folder with no blank passed on 6383c570. The cause was the harness, so the fix is there.
- `UpstreamCaseRunner.RunAsync` now takes an optional `testsDirectory`, the value of `%PWD` with forward slashes, and refuses one holding a blank with an `ArgumentException`, as it already refused such a `%LOGDIR`. Left `null` (the default, chosen so existing callers compile unchanged), `%PWD` has no value and a case using it is skipped with a reason, as before. Pinned by `RunAsync_TestsDirectoryWithABlank_Throws`, `RunAsync_TestsDirectory_IsPwdWithForwardSlashes` and `RunAsync_NoTestsDirectory_SkipsACaseUsingPwd`.
- `UpstreamConformanceTests` still passes no tests directory, so the ratchet's skipped and passing sets are unchanged.
- Left for an interactive session (office work; lanes may not read `Gap/` or file tasks touching it): `Gap/Tools/Measure-UpstreamCases.cs` must pass `RunAsync` a tests folder with no blank (for example a copy of the release under the run folder); the next gap run should then measure `behaviour:test3009` as `match`. The first box is ticked for Curl's side; GF-0044 closes only on that re-measure (ADR-0433).
- Measure-CodeQuality.ps1 not run: the new branches (`testsDirectory` null or not, blank or not) are each reached by the new tests, and complexity is checked at build time.
- No option changed, so `--ai-help` needed no update.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. UpstreamCaseRunner owns PWD and refuses a tests directory with a blank; the measuring tool's change is left to an interactive session
