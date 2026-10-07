---
id: BL-1657
title: Cut ParallelProgressMeterText Size to five columns and Time to eight as curl's msnprintf buffers do
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1657 — Cut ParallelProgressMeterText Size to five columns and Time to eight as curl's msnprintf buffers do

## Goal

`ParallelProgressMeterText.Size` always returns five characters and `Time` always eight, cut as curl's fixed `msnprintf` buffers cut them, so a `-Z` progress line never grows past its columns.

## Context

- Found by BL-1504's adversarial tests. `Size((100L << 50) - 1)` returns `"99.10P"` (six characters): the tenths digit is `bytes % unitSize / (unitSize / 10)`, and because `unitSize / 10` rounds down it reaches 10 for the last few bytes below 100 of a unit. The same happens for M, G and T, e.g. `Size(100L * 1024 * 1024 - 1)` - a real 100 MB download.
- `Time(long.MaxValue)` returns `"106751991167300d"` (16 characters); any time over 9,999,999 days overflows the eight columns.
- Both doc comments promise "Five characters" / "Eight characters".
- curl 8.21.0's `src/tool_progress.c` computes the same digits but writes them with `msnprintf(max5, 6, ...)` and `msnprintf(r, 9, ...)`, which cut the text to 5 and 8 characters (so `"99.10"` without the unit letter, and `"10675199"`). Confirm against the source, and against real curl where a transfer can show it (`Record-CurlExchange.ps1`, a `-Z` download of just under 100 MiB), before pinning.

## Acceptance criteria

- [ ] `Size` returns exactly five characters for every value from 0 to `long.MaxValue`, matching curl's truncation, with tests at `(100L << 20) - 1`, `(100L << 50) - 1` and `long.MaxValue`.
- [ ] `Time` returns exactly eight characters for every value, matching curl's truncation, with a test at `long.MaxValue`.
- [ ] 100% line and branch coverage of `Curl.Output.UnitLibrary` holds; build clean, fast tests green.

## Notes

## Log

- 2026-10-07: Created.
