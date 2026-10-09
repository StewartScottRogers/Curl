---
id: GF-0024
title: --url-query passes a user's lower-case %3d through instead of normalising it to %3D
area: behaviour
key: behaviour:url-query-escape-case
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test1221]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
task:
tasks: []
---
# GF-0024 - --url-query passes a user's lower-case %3d through instead of normalising it to %3D

## Summary

Curl differs from upstream curl in behaviour: --url-query passes a user's lower-case %3d through instead of normalising it to %3D.

## Evidence

behaviour:test1221 expected 'upstream test1221 passes' (the reference curl exits 0), actual: <verify><protocol> differs at byte 131 (line 1): expected '...&%3D%3D HTTP/1.1', got '...&%3d%3d HTTP/1.1'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1221

## Suggestion

In Curl.Protocol.Abstractions.UnitLibrary's CurlUrl query building (used by --url-query in Curl.Cli.UnitLibrary), upper-case the hex digits of existing percent escapes when the query is appended, as curl's urlapi does with CURLU_APPENDQUERY.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
