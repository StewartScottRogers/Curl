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
completed:
---
# BL-1931 — Emulate tests/libtest/test610.pl (mkdir, rmdir, move, gone) for %PERL commands in the upstream case runner

## Goal

The upstream case runner emulates tests/libtest/test610.pl's `mkdir`, `rmdir`, `move` and `gone` verbs (and `rm`), so the cases that call it in a precheck, prepare or postcheck command are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs line ~170 returns "the harness does not run the Perl <code>"; UpstreamPerlSubstitution.cs interprets s/// strip lines - follow its style). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. BCL only, no Perl installed or required, platform-neutral, 100% line and branch coverage, complexity at most 10. Cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (curl 8.21.0); read upstream tests/libtest/*.pl from the curl-8.21.0 tarball into an empty scratch folder, never into the repository. Split out of BL-1894, which was too big for one lane run.

Forms used: `%PERL %SRCDIR/libtest/test610.pl mkdir|rmdir|gone <path>`, `move <from> <to>`, chained verbs on one line (`move a b rmdir c rm d`), and `%SRCDIR/libtest/test%TESTNUMBER.pl` where the case number is 610. Paths are `%PWD/%LOGDIR/...` and map into the runner's per-case log directory. Read test610.pl from the tarball for the exact semantics and exit codes.

## Acceptance criteria

- [ ] Unit tests cover each verb and a chained line, including the failure exit of `gone` on an existing path.
- [ ] A screening test pins that cases using only test610.pl no longer get "the harness does not run the Perl".
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10 per method.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states that the runner emulates test610.pl.

## Notes

## Log

- 2026-10-09: Created.
