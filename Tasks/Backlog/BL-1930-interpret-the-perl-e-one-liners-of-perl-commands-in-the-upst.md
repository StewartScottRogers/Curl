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
completed:
---
# BL-1930 — Interpret the perl -e one-liners of %PERL commands in the upstream case runner

## Goal

The upstream case runner interprets the distinct `%PERL -e '...'` one-liners the vendored cases use, so those cases are measured instead of skipped.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs line ~170 returns "the harness does not run the Perl <code>"; UpstreamPerlSubstitution.cs interprets s/// strip lines - follow its style). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. BCL only, no Perl installed or required, platform-neutral, 100% line and branch coverage, complexity at most 10. Cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (curl 8.21.0); read upstream tests/libtest/*.pl from the curl-8.21.0 tarball into an empty scratch folder, never into the repository. Split out of BL-1894, which was too big for one lane run.

The one-liners (grep the UpstreamTestData for `%PERL -e`): `exit((stat("<file>"))[9] != <epoch>)` (mtime checks, three cases); `print '...' if('%CLIENTIP' ne '127.0.0.1')` and the `%HOSTIP` / `$^O` / `%CLIENT6IP` prechecks; `open(IN,$ARGV[0]); my $lines=grep(/.../, <IN>); exit ($lines != 2)`; the `for(1 .. 1000) { printf(...) } > file` generator; and the two `for my $i ((1..100))` write/verify loops. Implement each form by pattern, not a general Perl interpreter; any form that cannot be reproduced faithfully stays skipped with the form named.

## Acceptance criteria

- [ ] Unit tests in Curl.Conformance.UnitTests cover each one-liner form implemented.
- [ ] A screening test pins that cases using only implemented one-liners no longer get "the harness does not run the Perl"; any still skipped name their reason.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10 per method.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states which one-liner forms the runner interprets.

## Notes

## Log

- 2026-10-09: Created.
