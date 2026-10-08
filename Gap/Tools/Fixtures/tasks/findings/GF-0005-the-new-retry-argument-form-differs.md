---
id: GF-0005
title: The new retry argument form differs
area: options
key: options:the-new-retry-argument-form-differs
severity: Medium
status: open
scope: newest
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [options:--retry:argument]
touches: [Curl.Console]
task:
tasks: []
---
# GF-0005 - The new retry argument form differs

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0005: `curl --fixture GF-0005 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0005: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-01_0900: 1 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-options.
