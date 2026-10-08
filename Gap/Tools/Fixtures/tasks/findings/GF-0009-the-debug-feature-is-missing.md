---
id: GF-0009
title: The debug feature is missing
area: features
key: features:the-debug-feature-is-missing
severity: High
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [features:Debug]
touches: [Curl.Console]
task: BL-104
tasks: [BL-104]
---
# GF-0009 - The debug feature is missing

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0009: `curl --fixture GF-0009 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0009: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-01_0900: 1 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-features.
