---
id: BL-3205
title: Print inf and INF from CurlPrintf instead of hanging on infinity with a precision
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Misc/Printf/PrintfDoubleWriter.cs, Libcurl.UnitTests/Misc/Printf/CurlPrintfInfinityTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3205 — Print inf and INF from CurlPrintf instead of hanging on infinity with a precision

## Goal

`CurlPrintf` prints upstream's text for positive and negative infinity (and NaN) with every conversion, width, precision and flag, and never hangs.

## Context

Stewart reported on 2026-10-22 (Libcurl API conformance batch, report 28). Repro: `CurlPrintf.MSNPrintf(buf, "%.1f", double.PositiveInfinity)`, also `%.0F` and `%5.1F`: it never returns; upstream prints `inf` or `INF`. Cause: `CountTenfolds` in `Libcurl/Misc/Printf/PrintfDoubleWriter.cs` loops `for (value = number; value >= 10.0; value /= 10)`, and infinity divided by 10 is still infinity. Guard non-finite values with `double.IsFinite` before any digit loop. Measure upstream's curl_mprintf (lib557-style) for the exact text of +inf, -inf and NaN with `%f %F %e %E %g %G`, widths, precision and the `- + space 0 #` flags.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `Libcurl.UnitTests/Misc/Printf/CurlPrintfInfinityTests.cs` covers +inf with `%.0f`, `%.1f`, `%e` and `%g` and each width and flag combination, pinned to upstream's text.
- [ ] The same table for -inf and NaN.
- [ ] Every test in the file carries a `[Timeout]` so a regression fails instead of hanging the run.
- [ ] Build clean with -warnaserror, Libcurl fast tests pass, Measure-CodeQuality.ps1 -Library Libcurl 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b633b3a1 487e6a62 c54053f7 da628fdd.
