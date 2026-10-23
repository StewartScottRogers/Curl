---
id: BL-3412
title: Honour the runtests.pl -E exclude file and the mkdir, cp and echo precheck line in the C# interpretation of upstream runtests.pl (case 1182)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3410]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1169Script.cs, Curl.Conformance.UnitLibrary/UpstreamTest1182Script.cs, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1169.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1182.cs, Curl.Conformance.UnitTests/UpstreamTest1169ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamTest1182ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed:
---
# BL-3412 — Honour the runtests.pl -E exclude file and the mkdir, cp and echo precheck line in the C# interpretation of upstream runtests.pl (case 1182)

## Goal

Upstream case 1182 (`runtests.pl ... -E <exclude file> 1`) runs and passes on the conformance ratchet: the harness runs its `mkdir`, `cp` and `echo ... > file` precheck line, and the C# interpretation of `runtests.pl` honours `-E`.

## Context

ADR-0803 (BL-3406) decides 1182 as `interpret`: the C# runner honours `-E <file>` (a line `test:<n>:<reason>` skips test n, prints the reason on stdout, and the run exits 1), and the harness interprets the precheck line as `mkdir`, `cp` and `echo '...' > file` only, with `;` between them. Today the case skips with `the harness does not interpret the <client><precheck> line mkdir ...`: `UpstreamCaseScreening.cs` (around line 116, `UninterpretedCheck`) refuses the line and `UpstreamCaseRunner.cs` (`PrecheckSkipReasonAsync`, around line 587) runs precheck lines. The case is `Curl.Conformance.UnitTests/UpstreamTestData/test1182.curltest`; upstream's runner is https://github.com/curl/curl/blob/curl-8_22_0/tests/runtests.pl (look for `-E` and `exclude`). Every path the precheck names must stay inside the case's log directory, and `cp %SRCDIR/data/test1` copies from the vendored `UpstreamTestData` (the file there is `test1.curltest`; check how `%SRCDIR/data` maps).

`UpstreamPerlCommandScripts` keys each interpretation by script name, so `runtests.pl` can be registered once. BL-3410 (case 1169, `-L`) adds that registration, so this task extends BL-3410's `runtests.pl` interpretation with `-E` rather than registering a second one; the 1169 files are in `touches` for that.

## Acceptance criteria

- [ ] The `runtests.pl` interpretation honours `-E <file>`: a unit test in the interpretation's tests file pins that a `test:1:<reason>` line skips test 1, writes `<reason>` to stdout and returns exit code 1.
- [ ] A unit test in `UpstreamCaseScreeningTests.cs` pins that a precheck line made only of `mkdir`, `cp` and `echo '...' > file` commands joined by `;` is not reported as uninterpreted, and one naming any other command still is.
- [ ] A unit test in `UpstreamCaseRunnerTests.cs` pins that such a precheck line creates the directories, copies the file and writes the echoed text, all inside the case's log directory, and that a path outside it skips the case.
- [ ] The postcheck line `grep -q "<text>" <file>` is interpreted (only case 1182 uses it): unit tests in `UpstreamCaseScreeningTests.cs` and `UpstreamCaseRunnerTests.cs` pin that it is not reported as uninterpreted, passes when the file inside the log directory holds the text, and fails the case when it does not.
- [ ] `1182` is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and its ratchet row passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports no member past a gate.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-23 (lane 4): Claimed with template placeholders for Goal, Context and criteria, which no run can tick, so moved back to Backlog to write them (the board allows criteria edits only outside Doing). Found in the same pass: the script registry is a dictionary keyed by script name and BL-3410 registers `runtests.pl` for 1169, so a second registration here would collide; added BL-3410 to `depends-on` and its files to `touches`. The precheck needs `UpstreamCaseScreening.cs` and `UpstreamCaseRunner.cs` (and their tests), which no task in Doing on origin/work/dark-factory held; added them. The postcheck `grep -q "..." %LOGDIR/stdout1182` is not interpreted either (`UninterpretedCheck` in `UpstreamCaseScreening.cs` accepts only perl one-liners and %RESOLVE lines); only case 1182 uses `grep -q` and no task covers it, so it is in this task's criteria, in the same two files. The case has no server, so nothing else stands in the way.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Backlog. Depends on BL-3410: both cases run runtests.pl, which the script registry keys once, so -E extends BL-3410's runtests.pl interpretation; criteria were template placeholders and are written now
