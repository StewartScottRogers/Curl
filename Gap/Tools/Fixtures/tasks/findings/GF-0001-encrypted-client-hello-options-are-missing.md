---
id: GF-0001
title: Encrypted Client Hello options are missing
area: options
key: options:encrypted-client-hello-options-are-missing
severity: Critical
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [options:--ech, options:--ech:argument]
touches: [Curl.Console, Curl.Console.UnitTests, Gap/Tools]
task:
tasks: []
---
# GF-0001 - Encrypted Client Hello options are missing

## Summary

Fixture finding for New-TasksFromGaps.ps1's self-test.

## Evidence

Evidence text of GF-0001: `curl --fixture GF-0001 http://localhost/` exits 0 on the reference and 2 on Curl.

## Suggestion

Suggestion text of GF-0001: change Curl.Console so the fixture item measures match.

## Measurements

- 2026-10-01_0900: 2 of 2 items are gaps.

## Log

- 2026-10-01_0900: Opened by gap-options.
