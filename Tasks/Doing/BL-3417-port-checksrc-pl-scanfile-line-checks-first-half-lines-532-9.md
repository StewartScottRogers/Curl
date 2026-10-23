---
id: BL-3417
title: Port checksrc.pl ScanFile line checks, first half (lines 532-900, curl-8_22_0) to C#
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3416]
touches: [Curl.Conformance.UnitLibrary/UpstreamChecksrcScanFirstChecks.cs, Curl.Conformance.UnitTests/UpstreamChecksrcScanFirstChecksTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3417 — Port checksrc.pl ScanFile line checks, first half (lines 532-900, curl-8_22_0) to C#

## Goal

The first half of checksrc.pl's ScanFile line checks (script lines 532-900, curl-8_22_0) is ported to C#, giving the same warnings as the Perl script.

## Context

Upstream curl 8.22.0 (curl-8_22_0) scripts/checksrc.pl, run for the sample C file of upstream case 1185 by the conformance runner, which matches the script by file name (UpstreamCaseScreening.PerlScriptName). Library rules: Curl.Conformance.UnitLibrary/CLAUDE.md. Split from BL-3413; see its Notes (Deferred) for the measured decisions.

## Acceptance criteria

- [ ] A C# class in Curl.Conformance.UnitLibrary/UpstreamChecksrcScanFirstChecks.cs ports the line checks of checksrc.pl lines 532-900, in the script's order, with its Perl quirks kept (`goto comment` loops the comment stripping; `#line` skips the line counter and prev-line updates; `if($l)` is false for "0")
- [ ] Curl.Conformance.UnitTests/UpstreamChecksrcScanFirstChecksTests.cs covers it to 100% line and branch coverage and pins each warning text and column
- [ ] `dotnet build` and the fast tests pass

## Notes

- Split from BL-3413 (over its cost cap).
- Plugs into the seam BL-3416 defines. Cyclomatic complexity at most 10 per method: split the checks into small methods.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 60aaa5ff 5a7f33a1 8dd02beb.
