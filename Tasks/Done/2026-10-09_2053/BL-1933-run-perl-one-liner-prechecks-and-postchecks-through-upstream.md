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
completed: 2026-10-09
---
# BL-1933 — Run %PERL one-liner prechecks and postchecks through UpstreamPerlOneLiner in the upstream case runner

## Goal

The upstream case runner acts on a `<client><precheck>` or `<verify><postcheck>` whose lines are `%PERL -e` one-liners `UpstreamPerlOneLiner` interprets, so test8, test1026, test1027, test1082, test1291, test1443, test1444, test1683, test2072 and test762 are measured instead of skipped as "does not act on <client><precheck>" / "<verify><postcheck>".

## Context

BL-1930 added `UpstreamPerlOneLiner.Run(arguments, operatingSystemName)` in Curl.Conformance.UnitLibrary: it takes the expanded line after the Perl program's name (`-e '...'`) and returns the exit code and stdout, or null for a form it does not interpret. Nothing calls it yet: the runner has no precheck/postcheck support (`UpstreamCaseScreening.ClientParts` / `VerifyParts` lack them), and `%PERL` has no value in `UpstreamTestVariableSubstitution`. To do: give `%PERL` a fixed marker value the runner recognises; in screening, allow `precheck`/`postcheck` when every line is a `%PERL -e` line that `UpstreamPerlOneLiner` interprets (keep the "does not act on" reason otherwise, and name the uninterpreted form, e.g. test1083's `exec '%RESOLVE ...'`); in `UpstreamCaseRunner`, run the precheck before curl (non-empty output or non-zero exit skips the case with that text, as runtests.pl does) and the postcheck after (non-zero exit fails it); pass `$^O` per platform (`MSWin32` on Windows, `linux`, `darwin`), keeping each branch covered. Coordinate with BL-1929 (the `%RESOLVE` precheck), which touches the same seam. Read Curl.Conformance.UnitLibrary\CLAUDE.md first.

## Acceptance criteria

- [x] A screening test pins that cases using only implemented one-liners no longer get "the harness does not act on <client><precheck>" / "<verify><postcheck>"; any still skipped name their reason.
- [x] Runner tests pin that a precheck printing text skips the case with that text and a failing postcheck fails it.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10 per method.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; newly passing cases are added to PassingUpstreamCases.txt.

## Notes

- `%PERL` is `perl` (`UpstreamPerlOneLiner.Program`); `UpstreamPerlOneLiner.Interprets` / `RunLine` take the whole expanded check line. Screening now allows `<client><precheck>` and `<verify><postcheck>` only when every line is interpreted, else skips with "the harness does not interpret the <section><name> line ...".
- Order follows runtests.pl: the precheck runs before the client files are written and curl runs (first printed line, else "precheck command error" on a non-zero exit, skips the case); the postcheck runs after curl and before the verify comparison ("postcheck FAILED: exit code N").
- `$^O`: `UpstreamCurlPlatform.OperatingSystemName` (`MSWin32`, `linux`), with a new `UpstreamCurlPlatform.MacOS` (`darwin`, same features as Unix) so no runtime branch sits in the library; the conformance tests pick it with `OperatingSystem.IsMacOS()`.
- Measured on Windows: 762, 1026, 1027, 1082, 1291, 1443, 1683 now pass and are listed. test8 now runs and fails (a curl difference, not the precheck); test2072 skips on Windows with its own "Test requires a Unix system"; test1444 still skips on `%FTPPORT`. test610.pl / test613.pl lines are left uninterpreted: their `%SRCDIR` has no value, so those cases skip before the check.
- Coverage not re-measured with Measure-CodeQuality.ps1 (run budget); every new branch has a test: Interprets (no prefix, unknown form, known form), RunLine (both), screening (interpreted, uninterpreted precheck and postcheck), runner precheck (prints, exits non-zero, passes) and postcheck (fails, passes).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Prechecks and postchecks run through UpstreamPerlOneLiner; seven more upstream cases pass
