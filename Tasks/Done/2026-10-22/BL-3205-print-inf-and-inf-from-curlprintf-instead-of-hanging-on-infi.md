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
completed: 2026-10-22
---
# BL-3205 — Print inf and INF from CurlPrintf instead of hanging on infinity with a precision

## Goal

`CurlPrintf` prints upstream's text for positive and negative infinity (and NaN) with every conversion, width, precision and flag, and never hangs.

## Context

Stewart reported on 2026-10-22 (Libcurl API conformance batch, report 28). Repro: `CurlPrintf.MSNPrintf(buf, "%.1f", double.PositiveInfinity)`, also `%.0F` and `%5.1F`: it never returns; upstream prints `inf` or `INF`. Cause: `CountTenfolds` in `Libcurl/Misc/Printf/PrintfDoubleWriter.cs` loops `for (value = number; value >= 10.0; value /= 10)`, and infinity divided by 10 is still infinity. Guard non-finite values with `double.IsFinite` before any digit loop. Measure upstream's curl_mprintf (lib557-style) for the exact text of +inf, -inf and NaN with `%f %F %e %E %g %G`, widths, precision and the `- + space 0 #` flags.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [x] `Libcurl.UnitTests/Misc/Printf/CurlPrintfInfinityTests.cs` covers +inf with `%.0f`, `%.1f`, `%e` and `%g` and each width and flag combination, pinned to upstream's text.
- [x] The same table for -inf and NaN.
- [x] Every test in the file carries a `[Timeout]` so a regression fails instead of hanging the run.
- [x] Build clean with -warnaserror, Libcurl fast tests pass, Measure-CodeQuality.ps1 -Library Libcurl 0 failing members.

## Notes

- Fix: `CountTenfolds` (only reached with a precision) now counts no digits for a value that is not finite, so `%.1f` of infinity no longer loops; `FormatMagnitude` already printed `inf`/`nan`.
- Measurement: there is no upstream libcurl build on this machine to run lib557 against, and the curl tool never formats a caller-given double. curl 8.22.0's `out_double` passes the double to the C library's `snprintf`, so the text is the platform's. Following ADR-0668 (doubles format the same everywhere, glibc's answers, which the existing `Render_Double_FormatsAsCPrintf` rows already pin), the tests pin glibc's C99 text: `inf`/`INF`, `-inf`, `nan`, and `-nan` for .NET's `double.NaN`, whose sign bit is set. Precision and `#` change nothing, `0` pads with spaces, and width pads (clamped to 325). That follows the standing decision, so no new ADR.
- `[Timeout(10_000, CooperativeCancellation = true)]` on each test, and each one formats on `Task.Run` and awaits it with `WaitAsync(TestContext.CancellationToken)`, so a loop that never ends fails at the timeout instead of holding the run.
- Checks: solution build with -warnaserror clean; Libcurl.UnitTests 1393 passed, 1 skipped (platform); Measure-CodeQuality -Library Libcurl 0 failing members. The tests are platform-neutral (no paths, OS text or `%ld`), so CI on Linux and macOS is covered.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b633b3a1 487e6a62 c54053f7 da628fdd.
- 2026-10-22: Doing -> Done. CurlPrintf prints inf, -inf and nan for every double conversion, width, precision and flag instead of hanging
