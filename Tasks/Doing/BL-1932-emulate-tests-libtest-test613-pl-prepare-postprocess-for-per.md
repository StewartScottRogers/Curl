---
id: BL-1932
title: Emulate tests/libtest/test613.pl (prepare, postprocess) for %PERL commands in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1932 — Emulate tests/libtest/test613.pl (prepare, postprocess) for %PERL commands in the upstream case runner

## Goal

The upstream case runner emulates tests/libtest/test613.pl's `prepare` and `postprocess` verbs, so the FTP and SFTP directory-listing cases that call it are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs line ~170 returns "the harness does not run the Perl <code>"; UpstreamPerlSubstitution.cs interprets s/// strip lines - follow its style). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. BCL only, no Perl installed or required, platform-neutral, 100% line and branch coverage, complexity at most 10. Cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (curl 8.21.0); read upstream tests/libtest/*.pl from the curl-8.21.0 tarball into an empty scratch folder, never into the repository. Split out of BL-1894, which was too big for one lane run.

Forms used: `%PERL %SRCDIR/libtest/test613.pl prepare <dir>` (five cases), `postprocess <dir> <curl output> [<mtime>]` (four forms), and `%SRCDIR/libtest/test%TESTNUMBER.pl` where the case number is 613. postprocess rewrites a listing into a platform-independent form; read test613.pl from the tarball and reproduce it exactly. test1013.pl and test1022.pl compare against `../curl-config`, which Curl does not ship: they stay skipped with that reason named, unless a faithful stand-in exists.

## Acceptance criteria

- [ ] Unit tests cover prepare and each postprocess form, with expected bytes taken from test613.pl's logic.
- [ ] A screening test pins that cases using only test613.pl no longer get "the harness does not run the Perl"; test1013.pl and test1022.pl cases name their own skip reason.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10 per method.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states that the runner emulates test613.pl.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
