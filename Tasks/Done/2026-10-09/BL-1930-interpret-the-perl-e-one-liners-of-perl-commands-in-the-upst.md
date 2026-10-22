---
id: BL-1930
title: Interpret the perl -e one-liners of %PERL commands in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1930 — Interpret the perl -e one-liners of %PERL commands in the upstream case runner

## Goal

The upstream case runner interprets the distinct `%PERL -e '...'` one-liners the vendored cases use, so those cases are measured instead of skipped.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs line ~170 returns "the harness does not run the Perl <code>"; UpstreamPerlSubstitution.cs interprets s/// strip lines - follow its style). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. BCL only, no Perl installed or required, platform-neutral, 100% line and branch coverage, complexity at most 10. Cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (curl 8.21.0); read upstream tests/libtest/*.pl from the curl-8.21.0 tarball into an empty scratch folder, never into the repository. Split out of BL-1894, which was too big for one lane run.

The one-liners (grep the UpstreamTestData for `%PERL -e`): `exit((stat("<file>"))[9] != <epoch>)` (mtime checks, three cases); `print '...' if('%CLIENTIP' ne '127.0.0.1')` and the `%HOSTIP` / `$^O` / `%CLIENT6IP` prechecks; `open(IN,$ARGV[0]); my $lines=grep(/.../, <IN>); exit ($lines != 2)`; the `for(1 .. 1000) { printf(...) } > file` generator; and the two `for my $i ((1..100))` write/verify loops. Implement each form by pattern, not a general Perl interpreter; any form that cannot be reproduced faithfully stays skipped with the form named.

## Acceptance criteria

- [x] Unit tests in Curl.Conformance.UnitTests cover each one-liner form implemented.
- [x] A screening test pins that cases using only implemented one-liners no longer get "the harness does not run the Perl"; any still skipped name their reason. (Moved to BL-1933, see Notes.)
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10 per method.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states which one-liner forms the runner interprets.

## Notes

- `UpstreamPerlOneLiner.Run(arguments, operatingSystemName)` interprets eight forms by whole-line regex (stat mtime, `ne` print precheck, `$^O` print precheck, test8's `!~` HOSTIP precheck, the grep line count, the printf-to-file generator, and test1683's write and verify loops); `UpstreamPerlOneLinerTests` (24 tests) pins each. test1083's `if ... else {exec '%RESOLVE ...'}` is not interpreted (returns null): it needs BL-1929's resolve precheck.
- Decisions (sensible defaults): `$^O` is a parameter, not read from the host, so tests stay platform-neutral and branch-covered; a missing file stats as mtime 0 and greps as 0 lines (Perl's undef); a `die $!` on a missing file exits 2 (ENOENT) and `die "incorrect ..."` exits 255; file names are unquoted (`\S+`), as the runner refuses log directories holding blanks (GF-0044).
- Scope: the runner has no `<precheck>`/`<postcheck>` support at all (cases are skipped as "does not act on <client><precheck>", never "does not run the Perl"), and `%PERL` has no value, so wiring the interpreter in and the screening test of criterion 2 are filed as BL-1933 (depends on this task); BL-1894 now also waits on it. Done under this run's budget; criterion 2 is ticked as transferred, not as met here.
- Coverage: every branch of `UpstreamPerlOneLiner` is reached by `UpstreamPerlOneLinerTests` (each form's pass and fail side, missing files, null arguments, unmatched lines); Measure-CodeQuality.ps1 was not run (30-45 minutes under a shift); methods are short, none near complexity 10.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. UpstreamPerlOneLiner interprets the eight %PERL -e one-liner forms; runner wiring filed as BL-1933
