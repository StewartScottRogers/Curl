---
id: GF-0004
title: After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one
area: behaviour
key: behaviour:auth-retry-lost-when-server-closes-after-challenge
severity: Critical
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test64, behaviour:test69, behaviour:test76, behaviour:test90, behaviour:test153, behaviour:test388, behaviour:test1079, behaviour:test1095, behaviour:test1229, behaviour:test1437, behaviour:test2061, behaviour:test2062, behaviour:test2063, behaviour:test2076, behaviour:test2091]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1962
tasks: [BL-1797, BL-1962]
---
# GF-0004 - After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one

## Summary

Curl differs from upstream curl in behaviour: After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one.

## Evidence

Every item expects 'upstream test<N> passes'. test2061 actual: <verify><protocol> differs at byte 82 (line 6): expected 'GET /2061 HTTP/1.1', got the end. test64, test69, test76, test90, test1079, test1095, test1229, test1437, test2062, test2063 and test2076 have the same shape. test153 expected 'GET /1530001 HTTP/1.1', got 'GET /1530002 HTTP/1.1'; test388 and test2091 are the same (the retry for the first URL is missing). In every case the 401 carries swsclose with no Connection: close, and the passing twins without swsclose (test65, test2064) pass. Cause in code: Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.ExchangeWithRetriesAsync sends the retry on the same connection while KeepsAlive holds. When that connection then dies before any response byte, FailedOutcome only resends when DiedBeforeResponse holds, and that requires a pooled, reused connection (BL-336). A connection opened in this transfer gets no fresh retry. curl counts a connection it already used once as reused and retries it fresh. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 64,2061,153

## Suggestion

In Curl.Protocol.Http.UnitLibrary's HttpProtocolHandler, treat a connection that already carried a request in this transfer as reused for the died-before-response rule (DiedBeforeResponse / FailedOutcome). Then an authentication retry, or any retry sent on the same connection, that meets a closed connection before its response begins is sent once more on a fresh connection ('Connection died, retrying a fresh connect'), as curl 8.21.0 does. Pin it with a unit test where a 401 Digest challenge is followed by the peer closing.

## Measurements

- 2026-10-08_1640: 15 of 15 items are gaps.
- 2026-10-08_2029: 3 of 15 items are gaps.
- 2026-10-10_0657: 3 of 15 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1797.
- 2026-10-10_0756: Filed BL-1962.
