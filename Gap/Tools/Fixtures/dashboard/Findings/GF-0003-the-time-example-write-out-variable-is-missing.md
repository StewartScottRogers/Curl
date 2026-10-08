---
id: GF-0003
title: The time_example write-out variable is missing
area: writeout
key: writeout:time-example
severity: High
status: open
scope: newest
introduced-in: 8.22.0
opened: 2026-10-06_0900
closed:
regression: false
items: [writeout:time_example]
touches: [Curl.Console, Curl.Console.UnitTests]
task:
tasks: []
---
# GF-0003 - The time_example write-out variable is missing

## Summary

Fixture finding for Export-GapDashboardData.ps1's self-test.

## Evidence

Fixture only; nothing was measured.

## Suggestion

Add %{time_example} to the write-out variables.

## Measurements

- 2026-10-06_0900: fixture.

## Log

- 2026-10-06_0900: Opened by the fixture.
