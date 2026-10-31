---
id: GF-0007
title: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0
area: behaviour
platform: windows
key: behaviour:stdin-upload-of-unknown-size
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-13_1406
regression: false
items: [behaviour:test60, behaviour:test98, behaviour:test1068, behaviour:test1069, behaviour:test1072, behaviour:test1073]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
task: BL-2075
tasks: [BL-1800, BL-2003, BL-2075]
---
# GF-0007 - -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0

## Summary

Curl differs from upstream curl in behaviour: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0.

## Evidence

Every item expects 'upstream test<N> passes'. test1068 actual: <verify><protocol> differs at byte 84 (line 5): expected 'Transfer-Encoding: chunked', got 'Content-Length: 19'. test1072/1073: expected 'Transfer-Encoding: chunked', got 'Content-Length: 122'. test60: expected 'Transfer-Encoding: chunked', got 'Content-Length: 1'. test98 (-H 'Transfer-Encoding:' -H 'Content-Length: 14'): expected 'Expect: 100-continue', got an empty line. test1069 (-T - -0): expected exit code 25, got 52. The reference curl exits 25 on test1072/1073 and 56 on test1068/test60. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1068,98,1069

## Suggestion

In Curl.Protocol.Http.UnitLibrary (HttpRequestFraming, HttpRequestBodyWriter), treat a stdin upload as a body of unknown size. Over HTTP/1.1 frame it with Transfer-Encoding: chunked, even when the user gives a Content-Length header, unless the user empties Transfer-Encoding. Add Expect: 100-continue for an upload of unknown size. Over HTTP/1.0 (-0, or a 1.0 server after a redirect or auth retry) fail with exit 25, as curl's 'chunked transfer encoding not supported by HTTP/1.0'. Curl.Console's stdin reading must stream rather than buffer to give the unknown size.

## Measurements

- 2026-10-08_1640: 6 of 6 items are gaps.
- 2026-10-08_2029: 4 of 6 items are gaps.
- 2026-10-10_0657: 5 of 6 items are gaps.
- 2026-10-11_0936: 3 of 6 items are gaps.
- 2026-10-11_1950: 1 of 6 items are gaps.
- 2026-10-12_0951: 1 of 6 items are gaps.
- 2026-10-12_2042: 1 of 6 items are gaps.
- 2026-10-13_1406: 0 of 6 items are gaps.
- 2026-10-14_0904: 0 of 6 items are gaps.
- 2026-10-15_0304: 0 of 6 items are gaps.
- 2026-10-15_0543: 0 of 6 items are gaps.
- 2026-10-15_0751: 0 of 6 items are gaps.
- 2026-10-15_1657: 0 of 6 items are gaps.
- 2026-10-17_2225: 0 of 6 items are gaps.
- 2026-10-19_0804: 0 of 6 items are gaps.
- 2026-10-19_1329: 0 of 6 items are gaps.
- 2026-10-22_0148: 0 of 6 items are gaps.
- 2026-10-23_1637: 0 of 6 items are gaps.
- 2026-10-25_1542: 0 of 6 items are gaps.
- 2026-10-30_2103: 0 of 6 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1800.
- 2026-10-10_0756: Filed BL-2003.
- 2026-10-11_1029: Filed BL-2075.
- 2026-10-13_1406: Closed: run 2026-10-13_1406 measured every item as match or excluded.
