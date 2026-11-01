---
id: GF-0024
title: --url-query passes a user's lower-case %3d through instead of normalising it to %3D
area: behaviour
platform: windows
key: behaviour:url-query-escape-case
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-08_2029
regression: false
items: [behaviour:test1221]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
task: BL-1817
tasks: [BL-1817]
---
# GF-0024 - --url-query passes a user's lower-case %3d through instead of normalising it to %3D

## Summary

Curl differs from upstream curl in behaviour: --url-query passes a user's lower-case %3d through instead of normalising it to %3D.

## Evidence

behaviour:test1221 expected 'upstream test1221 passes' (the reference curl exits 0), actual: <verify><protocol> differs at byte 131 (line 1): expected '...&%3D%3D HTTP/1.1', got '...&%3d%3d HTTP/1.1'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1221

## Suggestion

In Curl.Protocol.Abstractions.UnitLibrary's CurlUrl query building (used by --url-query in Curl.Cli.UnitLibrary), upper-case the hex digits of existing percent escapes when the query is appended, as curl's urlapi does with CURLU_APPENDQUERY.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 0 of 1 items are gaps.
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
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1817.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
