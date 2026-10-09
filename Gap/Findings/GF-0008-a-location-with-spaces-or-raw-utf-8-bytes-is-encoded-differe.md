---
id: GF-0008
title: A Location with spaces or raw UTF-8 bytes is encoded differently from curl, or rejected
area: behaviour
key: behaviour:redirect-location-url-encoding
severity: Critical
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-08_2029
regression: false
items: [behaviour:test40, behaviour:test662, behaviour:test663, behaviour:test1138]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
task: BL-1801
tasks: [BL-1801]
---
# GF-0008 - A Location with spaces or raw UTF-8 bytes is encoded differently from curl, or rejected

## Summary

Curl differs from upstream curl in behaviour: A Location with spaces or raw UTF-8 bytes is encoded differently from curl, or rejected.

## Evidence

Every item expects 'upstream test<N> passes'. test40 (-L) actual: <verify><protocol> differs at byte 129 (line 6): expected 'GET /we/are/all/moo.html/?name=d+a+niel&testcase=/400002 HTTP/1.1', got 'GET /we/are/all/moo.html/?name=d%20a%20niel&testcase=/400002 HTTP/1.1' (a space in the query becomes +). test662/663 (Location 'http://example.net/tes t case=/6620002' through -x): expected 'GET http://example.net/tes%20t%20case=/6620002 HTTP/1.1', got the end (the redirect is not followed). test1138: expected '?name=%D8%A2%D8%BA...', got '?name=%C3%98%C2%A2...' (the raw bytes were read as Latin-1 and re-encoded as UTF-8). Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 40,662,1138

## Suggestion

In Curl.Protocol.Http.UnitLibrary's HttpRedirectLocation (with CurlUrl in Curl.Protocol.Abstractions.UnitLibrary), resolve a Location as curl's urlapi does with CURLU_URLENCODE. Keep the header value as raw bytes, not decoded as Latin-1. Percent-encode each byte >= 0x80 as itself. Encode a space as %20 in the path and as + in the query. Accept an absolute Location that contains spaces instead of abandoning the redirect.

## Measurements

- 2026-10-08_1640: 4 of 4 items are gaps.
- 2026-10-08_2029: 0 of 4 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1801.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
