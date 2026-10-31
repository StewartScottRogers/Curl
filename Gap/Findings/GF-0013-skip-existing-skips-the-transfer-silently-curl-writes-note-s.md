---
id: GF-0013
title: --skip-existing skips the transfer silently; curl writes 'Note: skips transfer, "<file>" exists locally'
area: behaviour
platform: windows
key: behaviour:skip-existing-note-missing
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-11_0936
regression: false
items: [behaviour:test994, behaviour:test996, behaviour:test1491]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-2006
tasks: [BL-1806, BL-2006]
---
# GF-0013 - --skip-existing skips the transfer silently; curl writes 'Note: skips transfer, "<file>" exists locally'

## Summary

Curl differs from upstream curl in behaviour: --skip-existing skips the transfer silently; curl writes 'Note: skips transfer, "<file>" exists locally'.

## Evidence

Every item expects 'upstream test<N> passes'. test996 actual: <verify><stderr> differs at byte 0 (line 1): expected 'Note: skips transfer, "<LOGDIR>/there" exists locally', got the end. test994 (with globbing) and test1491 (file://) have the same shape. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 994,996,1491

## Suggestion

In Curl.Console, where --skip-existing decides to skip a transfer, write curl 8.21.0's note to stderr: 'Note: skips transfer, "<output path>" exists locally', one per skipped URL. Write it even under -s, as upstream's <verify><stderr> shows.

## Measurements

- 2026-10-08_1640: 3 of 3 items are gaps.
- 2026-10-08_2029: 3 of 3 items are gaps.
- 2026-10-10_0657: 3 of 3 items are gaps.
- 2026-10-11_0936: 0 of 3 items are gaps.
- 2026-10-11_1950: 0 of 3 items are gaps.
- 2026-10-12_0951: 0 of 3 items are gaps.
- 2026-10-12_2042: 0 of 3 items are gaps.
- 2026-10-13_1406: 0 of 3 items are gaps.
- 2026-10-14_0904: 0 of 3 items are gaps.
- 2026-10-15_0304: 0 of 3 items are gaps.
- 2026-10-15_0543: 0 of 3 items are gaps.
- 2026-10-15_0751: 0 of 3 items are gaps.
- 2026-10-15_1657: 0 of 3 items are gaps.
- 2026-10-17_2225: 0 of 3 items are gaps.
- 2026-10-19_0804: 0 of 3 items are gaps.
- 2026-10-19_1329: 0 of 3 items are gaps.
- 2026-10-22_0148: 0 of 3 items are gaps.
- 2026-10-23_1637: 0 of 3 items are gaps.
- 2026-10-25_1542: 0 of 3 items are gaps.
- 2026-10-30_2103: 0 of 3 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1806.
- 2026-10-10_0756: Filed BL-2006.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
