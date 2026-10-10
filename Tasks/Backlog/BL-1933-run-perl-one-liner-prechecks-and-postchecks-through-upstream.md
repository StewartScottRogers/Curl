---
id: BL-1933
title: Run %PERL one-liner prechecks and postchecks through UpstreamPerlOneLiner in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1930]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1933 — Run %PERL one-liner prechecks and postchecks through UpstreamPerlOneLiner in the upstream case runner

## Goal

The upstream case runner acts on a `<client><precheck>` or `<verify><postcheck>` whose lines are `%PERL -e` one-liners `UpstreamPerlOneLiner` interprets, so test8, test1026, test1027, test1082, test1291, test1443, test1444, test1683, test2072 and test762 are measured instead of skipped as "does not act on <client><precheck>" / "<verify><postcheck>".

## Context

BL-1930 added `UpstreamPerlOneLiner.Run(arguments, operatingSystemName)` in Curl.Conformance.UnitLibrary: it takes the expanded line after the Perl program's name (`-e '...'`) and returns the exit code and stdout, or null for a form it does not interpret. Nothing calls it yet: the runner has no precheck/postcheck support (`UpstreamCaseScreening.ClientParts` / `VerifyParts` lack them), and `%PERL` has no value in `UpstreamTestVariableSubstitution`. To do: give `%PERL` a fixed marker value the runner recognises; in screening, allow `precheck`/`postcheck` when every line is a `%PERL -e` line that `UpstreamPerlOneLiner` interprets (keep the "does not act on" reason otherwise, and name the uninterpreted form, e.g. test1083's `exec '%RESOLVE ...'`); in `UpstreamCaseRunner`, run the precheck before curl (non-empty output or non-zero exit skips the case with that text, as runtests.pl does) and the postcheck after (non-zero exit fails it); pass `$^O` per platform (`MSWin32` on Windows, `linux`, `darwin`), keeping each branch covered. Coordinate with BL-1929 (the `%RESOLVE` precheck), which touches the same seam. Read Curl.Conformance.UnitLibrary\CLAUDE.md first.

## Acceptance criteria

- [ ] A screening test pins that cases using only implemented one-liners no longer get "the harness does not act on <client><precheck>" / "<verify><postcheck>"; any still skipped name their reason.
- [ ] Runner tests pin that a precheck printing text skips the case with that text and a failing postcheck fails it.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10 per method.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; newly passing cases are added to PassingUpstreamCases.txt.

## Notes

## Log

- 2026-10-09: Created.
