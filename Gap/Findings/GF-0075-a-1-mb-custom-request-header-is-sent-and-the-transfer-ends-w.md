---
id: GF-0075
title: A 1 MB custom request header is sent and the transfer ends with exit 52, where curl refuses it with exit 100
area: behaviour
platform: windows
key: behaviour:oversized-custom-request-header-not-rejected
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-13_1406
closed: 2026-10-14_0904
regression: false
items: [behaviour:test3229]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2167
tasks: [BL-2167]
---
# GF-0075 - A 1 MB custom request header is sent and the transfer ends with exit 52, where curl refuses it with exit 100

## Summary

Curl differs from upstream curl in behaviour: A 1 MB custom request header is sent and the transfer ends with exit 52, where curl refuses it with exit 100.

## Evidence

behaviour:test3229 (-K - with stdin 'header X-Large: <1048576 x>', http://%HOSTIP:%HTTPPORT/3229): expected 'upstream test3229 passes' (exit 100, CURLE_TOO_LARGE, within a 6 MB allocation limit), actual '<verify><errorcode>: expected exit code 100, got 52'. Curl.Protocol.Http.UnitLibrary raises CurlExitCode.TooLarge only for response lines (HttpLineReader, HttpResponseHeadBuilder), never for the request it builds. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 3229

## Suggestion

In Curl.Protocol.Http.UnitLibrary, cap the request head as curl 8.22.0's request buffer does: when the head HttpRequestHeadFormatter builds (custom headers included) grows past curl's limit, fail before sending with exit 100 (CurlExitCode.TooLarge) and curl's message, both measured on the reference with Record-CurlExchange.ps1. Add a unit test with a 1 MB -H value.

## Measurements

- 2026-10-13_1406: 1 of 1 items are gaps.
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

- 2026-10-13_1406: Opened by gap-behaviour.
- 2026-10-13_1457: Filed BL-2167.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
