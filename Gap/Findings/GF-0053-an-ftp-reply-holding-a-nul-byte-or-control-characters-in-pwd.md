---
id: GF-0053
title: An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8
area: behaviour
platform: windows
key: behaviour:ftp-control-reply-not-validated
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_0936
regression: false
items: [behaviour:test3217, behaviour:test3218, behaviour:test2108]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
task: BL-1983
tasks: [BL-1983]
---
# GF-0053 - An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8

## Summary

Curl differs from upstream curl in behaviour: An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8.

## Evidence

Every item expects 'upstream test<N> passes'. test3217/3218 (PWD answered '257' with a path holding byte 0x03): '<verify><protocol> differs at byte 43 (line 4): expected the end, got "EPSV\r\n"'; upstream expects exit 8. test2108 (PASS answered '230 logged' NUL ' in'): expected the end, got 'PWD'; upstream expects exit 8. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 3217,3218,2108

## Suggestion

In Curl.Protocol.Ftp.UnitLibrary's control-reply reader, fail with exit 8 (weird server reply) on a reply line holding a NUL byte. In the PWD reply parser, refuse a path holding control characters with exit 8, sending nothing more, as curl 8.21.0 does.

## Measurements

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
- 2026-10-31_1828: 0 of 3 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1983.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
