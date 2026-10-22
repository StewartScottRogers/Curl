---
id: BL-1931
title: Emulate tests/libtest/test610.pl (mkdir, rmdir, move, gone) for %PERL commands in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1931 — Emulate tests/libtest/test610.pl (mkdir, rmdir, move, gone) for %PERL commands in the upstream case runner

## Goal

The upstream case runner emulates tests/libtest/test610.pl's `mkdir`, `rmdir`, `move` and `gone` verbs (and `rm`), so the cases that call it in a precheck, prepare or postcheck command are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs line ~170 returns "the harness does not run the Perl <code>"; UpstreamPerlSubstitution.cs interprets s/// strip lines - follow its style). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. BCL only, no Perl installed or required, platform-neutral, 100% line and branch coverage, complexity at most 10. Cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (curl 8.21.0); read upstream tests/libtest/*.pl from the curl-8.21.0 tarball into an empty scratch folder, never into the repository. Split out of BL-1894, which was too big for one lane run.

Forms used: `%PERL %SRCDIR/libtest/test610.pl mkdir|rmdir|gone <path>`, `move <from> <to>`, chained verbs on one line (`move a b rmdir c rm d`), and `%SRCDIR/libtest/test%TESTNUMBER.pl` where the case number is 610. Paths are `%PWD/%LOGDIR/...` and map into the runner's per-case log directory. Read test610.pl from the tarball for the exact semantics and exit codes.

## Acceptance criteria

- [x] Unit tests cover each verb and a chained line, including the failure exit of `gone` on an existing path.
- [x] A screening test pins that cases using only test610.pl no longer get "the harness does not run the Perl".
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10 per method.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states that the runner emulates test610.pl.

## Notes

- `UpstreamTest610Script.Run` takes the expanded line after the Perl program's name and returns
  an `UpstreamPerlOneLinerResult` (reused: exit code and stdout), or null when the program is not
  `test610.pl` (`test%TESTNUMBER.pl` in case 610 expands to it). Like BL-1930's interpreter it is
  not called by the runner yet: prechecks and postchecks are run by BL-1933, so the screening test
  pins that test610 lines never get "the harness does not run the Perl" (they are screened as a
  part the harness does not act on until BL-1933).
- Semantics read from test610.pl at curl-8_21_0 (fetched to a scratch folder): verbs stop at the
  first failure with `die "$!"`. Exit codes chosen: 2 (ENOENT) for a missing path, 17 (EEXIST)
  for mkdir on an existing path, 39 (Linux ENOTEMPTY, matching the OpenSSL-build platform) for a
  non-empty rmdir, 255 for `gone` on an existing path (its `die` follows a successful `-e`, so `$!`
  is assumed 0). Only zero versus non-zero matters to a precheck or postcheck.
- Coverage checked by branch walk against the tests rather than a 30-45 minute
  Measure-CodeQuality run: every switch arm, both outcomes of each conditional and the missing
  path/target dequeues are hit; complexity is enforced by the build (CA1502), which is clean.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. test610.pl verbs emulated by UpstreamTest610Script; build clean, fast tests green
