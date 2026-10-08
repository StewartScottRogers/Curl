---
id: GF-0008
title: The NO_PROXY wildcard differs
area: environment
key: environment:the-no-proxy-wildcard-differs
severity: Medium
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [environment:NO_PROXY]
touches: [Curl.Console]
task:
tasks: []
---
# GF-0008 - The NO_PROXY wildcard differs

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0008: `curl --fixture GF-0008 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0008: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-01_0900: 1 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-environment.
