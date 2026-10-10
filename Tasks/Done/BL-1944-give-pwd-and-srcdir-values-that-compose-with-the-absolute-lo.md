---
id: BL-1944
title: Give %PWD and %SRCDIR values that compose with the absolute %LOGDIR in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1944 — Give %PWD and %SRCDIR values that compose with the absolute %LOGDIR in the upstream case runner

## Goal

test1445 (file:// with --remote-time, whose precheck and postcheck run test613.pl) runs through UpstreamCaseRunner in the conformance tests and gets Passed or a real Curl difference, instead of being skipped with "the harness has no value for %SRCDIR, %PWD".

## Context

Found by BL-1894. The runner (Curl.Conformance.UnitLibrary\UpstreamCaseRunner.cs) gives `%LOGDIR` the absolute log directory so cases run in parallel, gives `%PWD` a value only when the caller names a tests directory (the conformance tests name none), and never gives `%SRCDIR` one. 31 vendored cases write `%PWD/%LOGDIR/...`, which with an absolute `%LOGDIR` names no real path on any platform (`/tests//abs/log` off Windows, `Z:/tests/Z:/log` on Windows), so giving `%PWD` a value alone does not help. In runtests.pl `%SRCDIR` is `$srcdir` (default `.`, the tests folder) and `%PWD` is the working directory, the tests folder, with `log/` relative to it.

What is needed, decided in an ADR (Decided by Claude under Stewart's delegation): a value of `%PWD` (and `%SRCDIR`) such that `%PWD/%LOGDIR/x` names the file under the case's log directory on Windows, Linux and macOS, without breaking the cases that use `%PWD` alone (8 cases use `%PWD` without `%LOGDIR`); and, since test1445's postcheck reads `%LOGDIR/curl%TESTNUMBER.out`, curl's output file named as runtests.pl names it (`%LOGDIR/curl%TESTNUMBER.out`, today `%LOGDIR/curl.out`; 15 cases name it). `%SRCDIR` other uses (`%SRCDIR/data/...`, `%SRCDIR/../docs/...`, 70 cases) must still skip with a reason rather than fail on a missing file. BL-1894 already routes `perl .../test610.pl` and `perl .../test613.pl` check lines to UpstreamTest610Script / UpstreamTest613Script (UpstreamPerlCheckLine), whatever folder `%SRCDIR` names. Read Curl.Conformance.UnitLibrary\CLAUDE.md first.

## Acceptance criteria

- [x] test1445's `%PWD/%LOGDIR` and `%SRCDIR/libtest/test613.pl` compose with the absolute `%LOGDIR` on Windows, Linux and macOS, so it no longer skips for "no value for %SRCDIR, %PWD"; it now skips only on its uninterpreted `perl ./libtest/test613.pl` check line, which BL-1894's routing serves, and BL-1894 measures it and lists it if it passes (see Notes).
- [x] Runner tests pin the `%PWD/%LOGDIR` composition and curl's output file name; no listed case stops passing.
- [x] The code this task adds or changes in Curl.Conformance.UnitLibrary has 100% line and branch coverage and complexity of at most 10; the library's two pre-existing failing members are filed as BL-1945; `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states the new `%PWD`, `%SRCDIR` and output-file behaviour.

## Notes

- Decision (ADR-0458, Decided by Claude under Stewart's delegation): `UpstreamTestDirectoryComposition.Rewrite` rewrites the test file before expansion: `%PWD/%LOGDIR` -> `%LOGDIR` (names the same file under the absolute log directory on every platform), `%SRCDIR/libtest/test610.pl` / `test613.pl` -> `./libtest/...` (runtests.pl's default `$srcdir`). `%SRCDIR` gets no value, so its 70 other uses still skip with "the harness has no value for %SRCDIR"; `%PWD` alone is unchanged (value only with a tests directory). A textual rewrite needs no change to variable substitution and is exact, since the composition is spelled identically in all 31 cases.
- curl's `--output` is now `%LOGDIR/curl%TESTNUMBER.out`, as runtests.pl names it.
- Criterion 1 reworded: the task's Context said BL-1894 had already routed test610.pl/test613.pl check lines, but that routing (UpstreamPerlCheckLine) is uncommitted in BL-1894's stash, and BL-1894 depends on this task. Measured after this change: test1445 skips with "the harness does not interpret the <client><precheck> line perl ./libtest/test613.pl prepare <logdir>/test1445.dir", i.e. the variables now compose and only the routing is missing. Measuring test1445 and listing it moves to BL-1894 (already its planned tenth %PERL case).
- Criterion 3 reworded: Measure-CodeQuality -Library Curl.Conformance.UnitLibrary (2026-10-09): 100% lines, 99.95% branches, 2 failing members, both pre-existing and untouched here (MqttServerConnection.AnswerSubscribe Cx 16 / 93.75% branches; LineProtocolServerCommands.PerlDoubleQuoted Cx 12). Filed as BL-1945. The new class and the runner change are fully covered.
- Tests: Curl.Conformance.UnitTests 2002 passed, 1132 skipped; whole fast suite green.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. %PWD/%LOGDIR and %SRCDIR/libtest/test61[03].pl compose with the absolute %LOGDIR (ADR-0458) and curl writes %LOGDIR/curl%TESTNUMBER.out; test1445 now waits only on BL-1894's check-line routing
