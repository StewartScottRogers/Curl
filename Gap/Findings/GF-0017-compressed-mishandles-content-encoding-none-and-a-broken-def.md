---
id: GF-0017
title: --compressed mishandles 'Content-Encoding: none' and a broken deflate header
area: behaviour
key: behaviour:content-encoding-edge-cases
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-08_2029
regression: false
items: [behaviour:test223, behaviour:test328]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1810
tasks: [BL-1810]
---
# GF-0017 - --compressed mishandles 'Content-Encoding: none' and a broken deflate header

## Summary

Curl differs from upstream curl in behaviour: --compressed mishandles 'Content-Encoding: none' and a broken deflate header.

## Evidence

Both items expect 'upstream test<N> passes'. test328 (Content-Encoding: none): the --output file against <reply><data> differs at byte 119 (line 7): expected 'Q- What did 0 say to 8? A- Nice Belt!', got the end. test223 (broken deflate header): stderr differs from the reference curl, which exits 61. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 223,328

## Suggestion

In Curl.Protocol.Http.UnitLibrary's HttpContentCoding / HttpContentDecoder: treat 'none' (like 'identity') as no coding and pass the body through. For a deflate stream with a bad header, fail with exit 61 and curl 8.21.0's exact message ('Error while processing content unencoding: ...').

## Measurements

- 2026-10-08_1640: 2 of 2 items are gaps.
- 2026-10-08_2029: 0 of 2 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1810.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
