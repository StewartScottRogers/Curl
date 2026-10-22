# ADR-0724 — A `-Z` progress time past 999 days follows curl 8.22.0's `timebuf`

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2899
- Decided by Claude under Stewart's delegation.

## Context

`ParallelProgressMeterText.Time` followed curl 8.21.0's `time2str`: past 999 days it wrote days
alone, cut to eight characters (ADR-0428), so `long.MaxValue` seconds was `10675199`. curl 8.22.0
renamed the function `timebuf` and, past 999 days, writes months of 30 days (`%3dm %02dd`) up to
999 months, then years of 365 days (`%7dy`) up to 99999 years, then ` >99999y`
(`src/tool_progress.c` at `curl-8_22_0`). Upstream unit test 1622 (`tests/tunit/tool1622.c`) pins
those values, and BL-2845 ports it.

No `-Z` progress test in this repository pins the 8.21.0 days-alone text, and no real transfer
reaches 1000 days of estimated time, so there is nothing to measure against a running curl; the
C source and test 1622's expected stdout are the measurement.

## Decision

`Time` writes what 8.22.0's `timebuf` writes for every input: `hh:mm:ss`, `ddd hhh`, `mmmm ddd`,
`yyyyyyyy` and ` >99999y`. Each layout is exactly eight characters, so ADR-0428's cut to eight is
no longer needed for times (it still holds for `max5data` sizes).

## Why

The upstream test data Curl's conformance ratchet runs is 8.22.0's, and the new layouts never
overflow the column. The 8.21.0 text differs only past 999 days of estimated time, which no
script can observe from a real transfer.
