---
id: GF-0001
title: Encrypted Client Hello options are missing
area: options
key: options:encrypted-client-hello
severity: High
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-01_0900
closed:
regression: false
items: [options:--ech, options:--ech:argument]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1800
tasks: [BL-1800]
---
# GF-0001 - Encrypted Client Hello options are missing

## Summary

Fixture finding for Export-GapDashboardData.ps1's self-test.

## Evidence

Fixture only; nothing was measured.

## Suggestion

Parse --ech and its four argument forms in Curl.Console and pass them to the TLS layer.

## Measurements

- 2026-10-01_0900: fixture.

## Log

- 2026-10-01_0900: Opened by the fixture.
