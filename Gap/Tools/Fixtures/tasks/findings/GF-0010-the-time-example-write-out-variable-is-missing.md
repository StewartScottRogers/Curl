---
id: GF-0010
title: The time_example write-out variable is missing
area: writeout
key: writeout:the-time-example-write-out-variable-is-missing
severity: High
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [writeout:time_example]
touches: [Curl.Console]
task:
tasks: []
---
# GF-0010 - The time_example write-out variable is missing

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0010: `curl --fixture GF-0010 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0010: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-01_0900: 1 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-writeout.
