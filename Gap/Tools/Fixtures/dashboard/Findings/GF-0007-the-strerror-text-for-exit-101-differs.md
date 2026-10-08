---
id: GF-0007
title: The strerror text for exit 101 differs
area: exitcodes
key: exitcodes:strerror-101
severity: Medium
status: closed
scope: target
introduced-in: 8.12.0
opened: 2026-10-02_0900
closed: 2026-10-05_0900
regression: false
items: [exitcodes:101:strerror]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1803
tasks: [BL-1803]
---
# GF-0007 - The strerror text for exit 101 differs

## Summary

Fixture finding for Export-GapDashboardData.ps1's self-test.

## Evidence

Fixture only; nothing was measured.

## Suggestion

Copy curl's strerror text for exit 101.

## Measurements

- 2026-10-02_0900: fixture.

## Log

- 2026-10-02_0900: Opened by the fixture.
