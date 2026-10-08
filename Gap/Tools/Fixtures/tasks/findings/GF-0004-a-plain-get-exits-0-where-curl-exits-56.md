---
id: GF-0004
title: A plain GET exits 0 where curl exits 56
area: behaviour
key: behaviour:a-plain-get-exits-0-where-curl-exits-56
severity: Critical
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [behaviour:test0001]
touches: [Curl.Console]
task:
tasks: [BL-102]
---
# GF-0004 - A plain GET exits 0 where curl exits 56

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0004: `curl --fixture GF-0004 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0004: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-05_1200: 1 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-behaviour.
