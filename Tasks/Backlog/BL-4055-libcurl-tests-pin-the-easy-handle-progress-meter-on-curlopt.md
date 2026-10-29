---
id: BL-4055
title: Libcurl tests pin the easy handle progress meter on CURLOPT_STDERR and pass the Libcurl quality gate
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4018, BL-4054]
touches: [Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4055 — Libcurl tests pin the easy handle progress meter on CURLOPT_STDERR and pass the Libcurl quality gate

## Goal

A Libcurl easy handle with `CURLOPT_NOPROGRESS=0` is shown by Libcurl.UnitTests to draw curl's built-in progress meter on `CURLOPT_STDERR` when no callback is set or the callback returns `CURL_PROGRESSFUNC_CONTINUE`, and nothing when it returns 0.

## Context

Split from BL-4012 (over its cost cap); needs BL-4054, which makes the Engine draw the meter. The tests live in `Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.cs` (3 tests existed in the stashed work of BL-4012, which BL-4054 continues; if the file is already present after BL-4054, extend and verify it). Libcurl quality was never run. Measure real libcurl's stderr bytes for both cases before pinning them.

## Acceptance criteria

- [ ] A Libcurl.UnitTests test performs an easy transfer with `CURLOPT_NOPROGRESS=0`, no progress callback and `CURLOPT_STDERR` set, and finds curl's meter header lines and a final status line in what was written to it.
- [ ] A Libcurl.UnitTests test with an XFERINFOFUNCTION returning `CURL_PROGRESSFUNC_CONTINUE` finds the meter drawn, and one returning 0 finds nothing written.
- [ ] `dotnet build` is clean and the fast tests are green.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports no member this task changed outside a gate.

## Notes

- Split from BL-4012.

## Log

- 2026-10-29: Created.
