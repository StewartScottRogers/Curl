---
id: GF-0012
title: Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)
area: behaviour
platform: windows
key: behaviour:http-response-parsing-exit-codes
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-11_0936
regression: false
items: [behaviour:test1144, behaviour:test1473, behaviour:test1480, behaviour:test2106]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2005
tasks: [BL-1805, BL-2005]
---
# GF-0012 - Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)

## Summary

Curl differs from upstream curl in behaviour: Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer).

## Evidence

Every item expects 'upstream test<N> passes'. test1144 (-I --http0.9, body only): <verify><errorcode> expected exit code 8, got 0. test1473 (first header line starts with a space): expected 0, got 8. test1480 (HTTP/1.1 100 Continue, then body with no final head): expected 8, got 1. test2106 (NUL byte in a chunked trailer): expected 8, got 0. The reference curl agrees with upstream on test1144, test1473, test1480 and test2106. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1144,1473,1480,2106

## Suggestion

In Curl.Protocol.Http.UnitLibrary's HttpResponseHeadReader and HttpChunkedDecoder: (1) a HEAD (-I) answered by an HTTP/0.9 body is a weird server reply, exit 8; (2) fold, rather than reject, a first header line that starts with whitespace, as curl 8.21.0 does; (3) after a 1xx, bytes that do not start a status line are a weird server reply (8), not the HTTP/0.9 refusal (1); (4) a NUL byte in a trailer line fails with exit 8.

## Measurements

- 2026-10-08_1640: 4 of 4 items are gaps.
- 2026-10-08_2029: 1 of 4 items are gaps.
- 2026-10-10_0657: 1 of 4 items are gaps.
- 2026-10-11_0936: 0 of 4 items are gaps.
- 2026-10-11_1950: 0 of 4 items are gaps.
- 2026-10-12_0951: 0 of 4 items are gaps.
- 2026-10-12_2042: 0 of 4 items are gaps.
- 2026-10-13_1406: 0 of 4 items are gaps.
- 2026-10-14_0904: 0 of 4 items are gaps.
- 2026-10-15_0304: 0 of 4 items are gaps.
- 2026-10-15_0543: 0 of 4 items are gaps.
- 2026-10-15_0751: 0 of 4 items are gaps.
- 2026-10-15_1657: 0 of 4 items are gaps.
- 2026-10-17_2225: 0 of 4 items are gaps.
- 2026-10-19_0804: 0 of 4 items are gaps.
- 2026-10-19_1329: 0 of 4 items are gaps.
- 2026-10-22_0148: 0 of 4 items are gaps.
- 2026-10-23_1637: 0 of 4 items are gaps.
- 2026-10-25_1542: 0 of 4 items are gaps.
- 2026-10-30_2103: 0 of 4 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1805.
- 2026-10-10_0756: Filed BL-2005.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
