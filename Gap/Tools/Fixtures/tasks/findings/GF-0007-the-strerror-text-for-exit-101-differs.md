---
id: GF-0007
title: The strerror text for exit 101 differs
area: exitcodes
key: exitcodes:the-strerror-text-for-exit-101-differs
severity: Medium
status: closed
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [exitcodes:101]
touches: [Curl.Console]
task:
tasks: []
---
# GF-0007 - The strerror text for exit 101 differs

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0007: `curl --fixture GF-0007 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0007: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-01_0900: 0 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-exitcodes.
