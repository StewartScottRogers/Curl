---
id: GF-0002
title: The mqtts scheme is missing
area: protocols
key: protocols:the-mqtts-scheme-is-missing
severity: Medium
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [protocols:mqtts]
touches: [Gap/Findings, Audit/Findings, .claude/agents/audit-quality.md]
task:
tasks: []
---
# GF-0002 - The mqtts scheme is missing

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0002: `curl --fixture GF-0002 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0002: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-01_0900: 1 of 1 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-protocols.
