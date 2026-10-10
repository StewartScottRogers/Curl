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
completed:
---
# BL-1944 — Give %PWD and %SRCDIR values that compose with the absolute %LOGDIR in the upstream case runner

## Goal

test1445 (file:// with --remote-time, whose precheck and postcheck run test613.pl) runs through UpstreamCaseRunner in the conformance tests and gets Passed or a real Curl difference, instead of being skipped with "the harness has no value for %SRCDIR, %PWD".

## Context

Found by BL-1894. The runner (Curl.Conformance.UnitLibrary\UpstreamCaseRunner.cs) gives `%LOGDIR` the absolute log directory so cases run in parallel, gives `%PWD` a value only when the caller names a tests directory (the conformance tests name none), and never gives `%SRCDIR` one. 31 vendored cases write `%PWD/%LOGDIR/...`, which with an absolute `%LOGDIR` names no real path on any platform (`/tests//abs/log` off Windows, `Z:/tests/Z:/log` on Windows), so giving `%PWD` a value alone does not help. In runtests.pl `%SRCDIR` is `$srcdir` (default `.`, the tests folder) and `%PWD` is the working directory, the tests folder, with `log/` relative to it.

What is needed, decided in an ADR (Decided by Claude under Stewart's delegation): a value of `%PWD` (and `%SRCDIR`) such that `%PWD/%LOGDIR/x` names the file under the case's log directory on Windows, Linux and macOS, without breaking the cases that use `%PWD` alone (8 cases use `%PWD` without `%LOGDIR`); and, since test1445's postcheck reads `%LOGDIR/curl%TESTNUMBER.out`, curl's output file named as runtests.pl names it (`%LOGDIR/curl%TESTNUMBER.out`, today `%LOGDIR/curl.out`; 15 cases name it). `%SRCDIR` other uses (`%SRCDIR/data/...`, `%SRCDIR/../docs/...`, 70 cases) must still skip with a reason rather than fail on a missing file. BL-1894 already routes `perl .../test610.pl` and `perl .../test613.pl` check lines to UpstreamTest610Script / UpstreamTest613Script (UpstreamPerlCheckLine), whatever folder `%SRCDIR` names. Read Curl.Conformance.UnitLibrary\CLAUDE.md first.

## Acceptance criteria

- [ ] test1445 is measured (Passed or a real Curl difference) on Windows, Linux and macOS; if it passes it is on PassingUpstreamCases.txt.
- [ ] Runner tests pin the `%PWD/%LOGDIR` composition and curl's output file name; no listed case stops passing.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states the new `%PWD`, `%SRCDIR` and output-file behaviour.

## Notes

## Log

- 2026-10-09: Created.
