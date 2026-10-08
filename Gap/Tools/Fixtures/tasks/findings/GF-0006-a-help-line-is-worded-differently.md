---
id: GF-0006
title: A help line is worded differently
area: options
key: options:a-help-line-is-worded-differently
severity: Low
status: rejected
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [options:--help:text]
touches: [Curl.Console]
task:
tasks: []
---
# GF-0006 - A help line is worded differently

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0006: `curl --fixture GF-0006 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0006: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-01_0900: 1 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-options.
