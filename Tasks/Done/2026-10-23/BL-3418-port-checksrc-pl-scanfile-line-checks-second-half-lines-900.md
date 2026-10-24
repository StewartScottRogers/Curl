---
id: BL-3418
title: Port checksrc.pl ScanFile line checks, second half (lines 900-1268, curl-8_22_0) to C#
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3416, BL-3417]
touches: [Curl.Conformance.UnitLibrary/UpstreamChecksrcScanLastChecks.cs, Curl.Conformance.UnitTests/UpstreamChecksrcScanLastChecksTests.cs, Curl.Conformance.UnitLibrary/UpstreamChecksrcRun.cs, Curl.Conformance.UnitTests/UpstreamChecksrcRunTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3418 — Port checksrc.pl ScanFile line checks, second half (lines 900-1268, curl-8_22_0) to C#

## Goal

The second half of checksrc.pl's ScanFile line checks (script lines 900-1268, curl-8_22_0) is ported to C#, giving the same warnings as the Perl script.

## Context

Upstream curl 8.22.0 (curl-8_22_0) scripts/checksrc.pl, run for the sample C file of upstream case 1185 by the conformance runner, which matches the script by file name (UpstreamCaseScreening.PerlScriptName). Library rules: Curl.Conformance.UnitLibrary/CLAUDE.md. Split from BL-3413; see its Notes (Deferred) for the measured decisions.

## Acceptance criteria

- [x] A C# class in Curl.Conformance.UnitLibrary/UpstreamChecksrcScanLastChecks.cs ports the line checks of checksrc.pl lines 900-1268, in the script's order, with its Perl quirks kept (SPACEBEFOREPAREN's typedef test reads the keyword capture when `first` is `*`; the banned-function loop replaces `prefix.bad.suff` with `prefix` plus `x` times (len+1))
- [x] Curl.Conformance.UnitTests/UpstreamChecksrcScanLastChecksTests.cs covers it to 100% line and branch coverage and pins each warning text and column
- [x] `dotnet build` and the fast tests pass

## Notes

- Split from BL-3413 (over its cost cap).
- Plugs into the seam BL-3416 defines; depends on BL-3417 only so the two do not run together on the same seam.
- 2026-10-23 (lane 3): added `UpstreamChecksrcRun.cs` and `UpstreamChecksrcRunTests.cs` to touches (no task in Doing on origin/work/dark-factory names them): `CheckLineSecondHalf` becomes an instance method calling `UpstreamChecksrcScanLastChecks.Check(this, scan)`, and the frame test `Check_Instructions_DisableEnableAndReportBadOnes` now also expects the `EXCLAMATIONSPACE` warning at 4:14 for `// !checksrc! frob` (8 warnings), as real checksrc.pl at curl-8_22_0 gives it (measured with Git for Windows' perl on a sample file, which also confirmed the BRACEPOS, BANNEDFUNC loop, INDENTATION and USESAFEFREE columns pinned here).
- SPACEBEFOREPAREN (and its typedef test reading the keyword capture when `first` is `*`) sits before line 900 and was ported by BL-3417 (`IsFunctionPointer`); this task keeps the banned-function loop's quirk: each found call `prefix.bad.suff` is replaced by `prefix` plus `x` times (len+1) and the line searched again, only the first call after a non-word character (else after an open paren) is looked at, and the line is restored afterwards. Perl's `if($l)` is false for `"0"`, which keeps the last `curlx_free()` call. `b` in ERRNOVAR is spelled as ASCII lookarounds. No new behaviour choice, so no ADR.
- Platform-neutral: the tests use no OS text; the STDERR test writes `.checksrc` and a C file in a temporary directory it deletes, with forward-slash paths, so CI on Linux and macOS holds with the fast tests green here. Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members (after splitting `CheckNullAfterFree` out of `CheckSafeFree`).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 9da978a1 83e84198 8dd02beb.
- 2026-10-23: Doing -> Done. checksrc.pl scanfile line checks 900-1268 ported to C# (UpstreamChecksrcScanLastChecks), 100% covered, quality gates clean
