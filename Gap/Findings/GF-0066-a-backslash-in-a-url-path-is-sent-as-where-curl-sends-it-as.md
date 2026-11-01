---
id: GF-0066
title: A backslash in a URL path is sent as '/', where curl sends it as written
area: behaviour
platform: windows
key: behaviour:url-path-backslash-rewritten
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_0936
regression: false
items: [behaviour:test214]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
task: BL-1996
tasks: [BL-1996]
---
# GF-0066 - A backslash in a URL path is sent as '/', where curl sends it as written

## Summary

Curl differs from upstream curl in behaviour: A backslash in a URL path is sent as '/', where curl sends it as written.

## Evidence

behaviour:test214 (URL path written as backslash-brace, backslash-brace, backslash-slash, then 214) expected 'upstream test214 passes', actual '<verify><protocol> differs at byte 7 (line 1): expected "GET /{}\\/214 HTTP/1.1\r\n", got "GET /{}//214 HTTP/1.1\r\n"'. The glob escapes before { and } are removed, as they should be, but the backslash before '/' must stay. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 214

## Suggestion

In Curl.Cli.UnitLibrary's URL globbing (ADR-0032), unescape only the glob characters (a backslash before '[', ']', '{', '}', and ',' inside a set) and keep any other backslash. In Curl.Core.UnitLibrary's URL model, send a backslash in an http path as curl 8.21.0 does: as written, not turned into '/' the WHATWG way. Pin test214's request line.

## Measurements

- 2026-10-10_0657: 1 of 1 items are gaps.
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

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1996.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
