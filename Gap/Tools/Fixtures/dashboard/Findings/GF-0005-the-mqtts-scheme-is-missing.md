---
id: GF-0005
title: The mqtts scheme is missing
area: protocols
key: protocols:mqtts
severity: High
status: open
scope: target
introduced-in: 8.21.0
opened: 2026-10-02_0900
closed:
regression: false
items: [protocols:mqtts]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1802
tasks: [BL-1802]
---
# GF-0005 - The mqtts scheme is missing

## Summary

Fixture finding for Export-GapDashboardData.ps1's self-test.

## Evidence

Fixture only; nothing was measured.

## Suggestion

Add MQTT over TLS to Curl.Protocol.Mqtt.UnitLibrary.

## Measurements

- 2026-10-02_0900: fixture.

## Log

- 2026-10-02_0900: Opened by the fixture.
