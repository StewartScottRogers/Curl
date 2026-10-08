---
id: GF-0002
title: POST body is sent twice after a redirect
area: behaviour
key: behaviour:post-redirect-resend
severity: Critical
status: open
scope: target
introduced-in: 7.10
opened: 2026-10-01_0900
closed:
regression: true
items: [behaviour:test1000, behaviour:test1001, behaviour:test1002]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1801
tasks: [BL-1801, BL-1805]
---
# GF-0002 - POST body is sent twice after a redirect

## Summary

Fixture finding for Export-GapDashboardData.ps1's self-test.

## Evidence

Fixture only; nothing was measured.

## Suggestion

Send the request body once per request in Curl.Protocol.Http.UnitLibrary.
Keep "303" handling as it is; see docs\example.

## Measurements

- 2026-10-01_0900: fixture.

## Log

- 2026-10-01_0900: Opened by the fixture.
