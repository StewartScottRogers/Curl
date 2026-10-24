---
id: BL-3417
title: Port checksrc.pl ScanFile line checks, first half (lines 532-900, curl-8_22_0) to C#
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3416]
touches: [Curl.Conformance.UnitLibrary/UpstreamChecksrcScanFirstChecks.cs, Curl.Conformance.UnitTests/UpstreamChecksrcScanFirstChecksTests.cs, Curl.Conformance.UnitLibrary/UpstreamChecksrcRun.cs, Curl.Conformance.UnitTests/UpstreamChecksrcRunTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3417 — Port checksrc.pl ScanFile line checks, first half (lines 532-900, curl-8_22_0) to C#

## Goal

The first half of checksrc.pl's ScanFile line checks (script lines 532-900, curl-8_22_0) is ported to C#, giving the same warnings as the Perl script.

## Context

Upstream curl 8.22.0 (curl-8_22_0) scripts/checksrc.pl, run for the sample C file of upstream case 1185 by the conformance runner, which matches the script by file name (UpstreamCaseScreening.PerlScriptName). Library rules: Curl.Conformance.UnitLibrary/CLAUDE.md. Split from BL-3413; see its Notes (Deferred) for the measured decisions.

## Acceptance criteria

- [x] A C# class in Curl.Conformance.UnitLibrary/UpstreamChecksrcScanFirstChecks.cs ports the line checks of checksrc.pl lines 532-900, in the script's order, with its Perl quirks kept (`goto comment` loops the comment stripping; `#line` skips the line counter and prev-line updates; `if($l)` is false for "0")
- [x] Curl.Conformance.UnitTests/UpstreamChecksrcScanFirstChecksTests.cs covers it to 100% line and branch coverage and pins each warning text and column
- [x] `dotnet build` and the fast tests pass

## Notes

- Split from BL-3413 (over its cost cap).
- Plugs into the seam BL-3416 defines. Cyclomatic complexity at most 10 per method: split the checks into small methods.
- 2026-10-23 (lane 3): added `UpstreamChecksrcRun.cs` and `UpstreamChecksrcRunTests.cs` to touches (no task in Doing names them): the seam `CheckLineFirstHalf` becomes an instance method calling `UpstreamChecksrcScanFirstChecks.Check(this, scan)`, since the checks warn through the run; the frame test `Check_Instructions_DisableEnableAndReportBadOnes` now also gets the `CPPCOMMENTS` warning real checksrc.pl gives its `// !checksrc! frob` line.
- Seam for BL-3418: a preprocessor line (or one continuing a preprocessor line ending in a backslash) returns with `FileScan.Preprocessor` true, standing for `goto preproc`; BL-3418 runs its code checks only when it is false and the `BANNEDPREPROC` block when it is true. `$l` is unchanged after line 900, so BL-3418 recomputes `nostrings($l)`. The comma check (`COMMANOSPACE`, whose `if` is line 900) is left to BL-3418.
- Perl quirks kept and pinned: `s/.**///` is greedy (a line ending a comment loses everything up to its last `*/`); `[\">]` matches only quoted includes, so `#include <a.h>` twice is not INCLUDEDUP; `s/'?'//g` takes out every single quote; EQUALSNULL's lazy group keeps the space before `==`; a year-less copyright records column 0. Perl's `w`, `W`, `s` on byte strings are ASCII, so the patterns spell them out. Decided under the existing ADR-0803 (no new behaviour choice).
- Platform-neutral: the tests use no file system, path or OS text, so CI on Linux and macOS holds with the fast tests green here. Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members (after splitting `CheckSpaceBeforeParen` and `CheckCondition`).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 60aaa5ff 5a7f33a1 8dd02beb.
- 2026-10-23: Doing -> Done. checksrc.pl scanfile line checks 532-900 ported to C# (UpstreamChecksrcScanFirstChecks), 100% covered, quality gates clean
