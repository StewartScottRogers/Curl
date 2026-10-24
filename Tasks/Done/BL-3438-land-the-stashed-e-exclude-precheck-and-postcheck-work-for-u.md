---
id: BL-3438
title: Land the stashed -E exclude, precheck and postcheck work for upstream case 1182 and list 1182 on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3410]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1169Script.cs, Curl.Conformance.UnitLibrary/UpstreamShellCheckLine.cs, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1169.cs, Curl.Conformance.UnitTests/UpstreamTest1169ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamShellCheckLineTests.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3438 — Land the stashed -E exclude, precheck and postcheck work for upstream case 1182 and list 1182 on the ratchet

## Goal

Upstream case 1182 runs and passes on the conformance ratchet: the harness runs its `mkdir`, `cp` and `echo` precheck line and its `grep -q` postcheck, and the `runtests.pl` interpretation honours `-E`.

## Context

Split from BL-3412 (ADR-0803, BL-3406 decides 1182 as `interpret`). Upstream runner: https://github.com/curl/curl/blob/curl-8_22_0/tests/runtests.pl (`-E`, `exclude`). The case is `Curl.Conformance.UnitTests/UpstreamTestData/test1182.curltest`; every path the precheck names stays inside the case's log directory, and `cp %SRCDIR/data/test1` copies from the vendored `UpstreamTestData`. BL-3410 registers the `runtests.pl` interpretation (`UpstreamTest1169Script`); this extends it.

## Acceptance criteria

- [x] The `runtests.pl` interpretation honours `-E <file>`: a unit test in `UpstreamTest1169ScriptTests.cs` pins that a `test:1:<reason>` line skips test 1, writes `<reason>` to stdout and returns exit code 1.
- [x] A unit test in `UpstreamCaseScreeningTests.cs` pins that a precheck line made only of `mkdir`, `cp` and `echo '...' > file` commands joined by `;` is not reported as uninterpreted, and one naming any other command still is.
- [x] A unit test in `UpstreamCaseRunnerTests.cs` pins that such a precheck line creates the directories, copies the file and writes the echoed text, all inside the case's log directory, and that a path outside it skips the case.
- [x] The postcheck line `grep -q "<text>" <file>` is interpreted (only case 1182 uses it): unit tests in `UpstreamCaseScreeningTests.cs` and `UpstreamCaseRunnerTests.cs` pin that it is not reported as uninterpreted, passes when the file inside the log directory holds the text, and fails the case when it does not.
- [x] `1182` is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and its ratchet row passes.
- [x] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes and `dotnet build` is clean.

## Notes

Split from BL-3412, which hit its cost cap. Continues the stashed work of BL-3412.

The stash holds the finished code: new `UpstreamShellCheckLine.cs` and `UpstreamShellCheckLineTests.cs`, screening and runner wired to it, `UpstreamTest1169Script` honouring `-E` (prints `test NNNN SKIPPED: reason`, exit 1), tests added and 1182 listed; the Conformance fast run was 4321 passed, 0 failed, and the test1182 row passed. Remaining here: apply it, rerun that, tick the boxes and commit. The quality measurement and solution-wide fast tests are BL-3439.

Run 2 (lane 3): the applied stash built clean and the Conformance fast run passed (test1182 row passed). The lane rules require a quality run before production code is committed, so it ran here: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reported `UpstreamTest1169Script.Run` (complexity 22, branch 90.91%), `ExcludedRun` (complexity 16) and its field-count lambda (branch 50%). They were split into `OptionWordCount`, `ExcludedTestReasons`, `IsTestLine`, `ParseTestNumber` and `SkipReason`, with tests for the incomplete command lines and for short exclude lines. The exclude-line separator regex `s*:s*` was fixed to `\s*:\s*`, as `runtests.pl` splits it. The rerun shows 100% line and branch coverage, worst CRAP 10, no failing member. The Conformance fast run passes: 4354 passed, 0 failed. This meets BL-3439's quality criterion; its solution-wide fast run is left to that task. Platform check: every path stays in a temporary or log directory and paths are compared after `Path.GetFullPath`, so the output does not depend on the platform.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 16a6d8ed 84ffb677 ef020bad 406ec57e 45ba054b 22b8e47e.
- 2026-10-23: Doing -> Done. runtests.pl honours -E, case 1182's mkdir/cp/echo precheck and grep -q postcheck run, and 1182 passes on the ratchet
