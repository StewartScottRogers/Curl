---
id: GF-0003
title: The time_queue write-out variable is missing
area: writeout
key: writeout:the-time-queue-write-out-variable-is-missing
severity: Low
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [writeout:time_queue]
touches: [Curl.Console]
task:
tasks: [BL-101]
---
# GF-0003 - The time_queue write-out variable is missing

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0003: `curl --fixture GF-0003 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0003: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-09-25_0900: 1 of 1 items are gaps.
- 2026-10-05_1200: 1 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-writeout.
