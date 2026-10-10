---
id: GF-0012
title: Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)
area: behaviour
key: behaviour:http-response-parsing-exit-codes
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test1144, behaviour:test1473, behaviour:test1480, behaviour:test2106]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1805
tasks: [BL-1805]
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

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1805.
