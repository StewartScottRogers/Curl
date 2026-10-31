---
id: GF-0025
title: With Expect: 100-continue, the POST body is not sent when the server answers early and closes
area: behaviour
platform: windows
key: behaviour:expect-continue-body-after-early-response
severity: Critical
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-10_0657
regression: false
items: [behaviour:test1070]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1818
tasks: [BL-1818]
---
# GF-0025 - With Expect: 100-continue, the POST body is not sent when the server answers early and closes

## Summary

Curl differs from upstream curl in behaviour: With Expect: 100-continue, the POST body is not sent when the server answers early and closes.

## Evidence

behaviour:test1070 (-d @file -H 'Expect: 100-continue') expected 'upstream test1070 passes', actual: <verify><protocol> differs at byte 176 (line 9): expected 'This creates ', got the end. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1070

## Suggestion

In Curl.Protocol.Http.UnitLibrary's HttpContinueWaitConnection / HttpRequestBodyWriter: when the Expect wait times out or the server's final response has not yet arrived, start sending the body as curl 8.21.0 does. Then the bytes upstream's <verify><protocol> holds are written before the server's close is seen.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 1 of 1 items are gaps.
- 2026-10-10_0657: 0 of 1 items are gaps.
- 2026-10-11_0936: 0 of 1 items are gaps.
- 2026-10-11_1950: 0 of 1 items are gaps.
- 2026-10-12_0951: 0 of 1 items are gaps.
- 2026-10-12_2042: 0 of 1 items are gaps.
- 2026-10-13_1406: 0 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1818.
- 2026-10-10_0657: Closed: run 2026-10-10_0657 measured every item as match or excluded.
