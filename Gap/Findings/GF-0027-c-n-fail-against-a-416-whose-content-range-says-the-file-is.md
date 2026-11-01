---
id: GF-0027
title: -C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0
area: behaviour
platform: windows
key: behaviour:resume-complete-file-with-fail
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-08_2029
regression: false
items: [behaviour:test194]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1820
tasks: [BL-1820]
---
# GF-0027 - -C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0

## Summary

Curl differs from upstream curl in behaviour: -C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0.

## Evidence

behaviour:test194 (-C 87 --fail; reply 416 with 'Content-Range: bytes */87') expected 'upstream test194 passes' (the reference curl exits 0), actual: <verify><errorcode>: expected exit code 0, got 22. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 194

## Suggestion

In Curl.Protocol.Http.UnitLibrary's HttpDownloadConditions / HttpContentRange: treat a 416 whose 'Content-Range: bytes */<size>' equals the -C offset as 'the file is already complete'. Answer it before the -f check, with exit 0 and no body written, as curl 8.21.0 does.

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
- 2026-10-08_1731: Filed BL-1820.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
