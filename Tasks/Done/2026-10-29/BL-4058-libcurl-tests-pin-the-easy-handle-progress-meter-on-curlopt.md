---
id: BL-4058
title: Libcurl tests pin the easy handle progress meter on CURLOPT_STDERR and pass the Libcurl quality gate
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4018, BL-4057]
touches: [Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4058 — Libcurl tests pin the easy handle progress meter on CURLOPT_STDERR and pass the Libcurl quality gate

## Goal

A Libcurl easy handle with `CURLOPT_NOPROGRESS=0` is shown by Libcurl.UnitTests to draw curl's built-in progress meter on `CURLOPT_STDERR` when no callback is set or the callback returns `CURL_PROGRESSFUNC_CONTINUE`, and nothing when it returns 0.

## Context

Split from BL-4012 (over its cost cap); needs BL-4057, which makes the Engine draw the meter. The tests live in `Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.cs` (3 tests existed in the stashed work of BL-4012, which BL-4057 continues; if the file is already present after BL-4057, extend and verify it). Libcurl quality was never run. Measure real libcurl's stderr bytes for both cases before pinning them.

## Acceptance criteria

- [x] A Libcurl.UnitTests test performs an easy transfer with `CURLOPT_NOPROGRESS=0`, no progress callback and `CURLOPT_STDERR` set, and finds curl's meter header lines and a final status line in what was written to it.
- [x] A Libcurl.UnitTests test with an XFERINFOFUNCTION returning `CURL_PROGRESSFUNC_CONTINUE` finds the meter drawn, and one returning 0 finds nothing written.
- [x] `dotnet build` is clean and the fast tests are green.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports no member this task changed outside a gate.

## Notes

- Split from BL-4012.
- BL-4057 already added `CurlEasyProgressMeterTests.cs` with the three tests this task asks for; this run verified them rather than changing them. They pass (3/3).
- Measured real curl 8.22.0 (Schannel, mingw) on Windows: its default meter, which is libcurl's built-in meter, starts `  % Total    % Received % Xferd  Average Speed` and its second line holds `Dload  Upload`, which is what the tests pin. The tests check only those fragments, `\r100 ` and a trailing `Environment.NewLine`, so they do not depend on the platform. Nobody measured a libcurl C program directly, because a lane has no C toolchain; the curl tool draws this meter through libcurl, so its output stands in for that.
- `dotnet build` clean; fast tests green (exit 0). `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. No production code changed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5a165af6 a4efa820 e4d345af ae9d1d50.
- 2026-10-29: Doing -> Done. Libcurl tests pin the easy handle meter on CURLOPT_STDERR; Libcurl quality gate passes
