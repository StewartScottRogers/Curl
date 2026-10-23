---
id: BL-3418
title: Port checksrc.pl ScanFile line checks, second half (lines 900-1268, curl-8_22_0) to C#
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3416, BL-3417]
touches: [Curl.Conformance.UnitLibrary/UpstreamChecksrcScanLastChecks.cs, Curl.Conformance.UnitTests/UpstreamChecksrcScanLastChecksTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3418 — Port checksrc.pl ScanFile line checks, second half (lines 900-1268, curl-8_22_0) to C#

## Goal

The second half of checksrc.pl's ScanFile line checks (script lines 900-1268, curl-8_22_0) is ported to C#, giving the same warnings as the Perl script.

## Context

Upstream curl 8.22.0 (curl-8_22_0) scripts/checksrc.pl, run for the sample C file of upstream case 1185 by the conformance runner, which matches the script by file name (UpstreamCaseScreening.PerlScriptName). Library rules: Curl.Conformance.UnitLibrary/CLAUDE.md. Split from BL-3413; see its Notes (Deferred) for the measured decisions.

## Acceptance criteria

- [ ] A C# class in Curl.Conformance.UnitLibrary/UpstreamChecksrcScanLastChecks.cs ports the line checks of checksrc.pl lines 900-1268, in the script's order, with its Perl quirks kept (SPACEBEFOREPAREN's typedef test reads the keyword capture when `first` is `*`; the banned-function loop replaces `prefix.bad.suff` with `prefix` plus `x` times (len+1))
- [ ] Curl.Conformance.UnitTests/UpstreamChecksrcScanLastChecksTests.cs covers it to 100% line and branch coverage and pins each warning text and column
- [ ] `dotnet build` and the fast tests pass

## Notes

- Split from BL-3413 (over its cost cap).
- Plugs into the seam BL-3416 defines; depends on BL-3417 only so the two do not run together on the same seam.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 9da978a1 83e84198 8dd02beb.
