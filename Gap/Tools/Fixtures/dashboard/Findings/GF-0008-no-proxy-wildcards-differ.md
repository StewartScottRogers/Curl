---
id: GF-0008
title: NO_PROXY wildcards differ
area: environment
key: environment:no-proxy-wildcards
severity: Medium
status: closed
scope: target
introduced-in: 7.86.0
opened: 2026-10-01_0900
closed: 2026-10-01_0900
regression: false
items: [environment:NO_PROXY]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1804
tasks: [BL-1804]
---
# GF-0008 - NO_PROXY wildcards differ

## Summary

Fixture finding for Export-GapDashboardData.ps1's self-test.

## Evidence

Fixture only; nothing was measured.

## Suggestion

Match curl's NO_PROXY wildcard rules.

## Measurements

- 2026-10-01_0900: fixture.

## Log

- 2026-10-01_0900: Opened by the fixture.
