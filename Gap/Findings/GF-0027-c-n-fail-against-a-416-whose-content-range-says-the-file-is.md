---
id: GF-0027
title: -C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0
area: behaviour
key: behaviour:resume-complete-file-with-fail
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test194]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1820
tasks: [BL-1820]
---
# GF-0027 - -C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0

## Summary

Curl differs from upstream curl in behaviour: -C N --fail against a 416 whose Content-Range says the file is complete exits 22 instead of 0.

## Evidence

behaviour:test194 (-C 87 --fail; reply 416 with 'Content-Range: bytes */87') expected 'upstream test194 passes' (the reference curl exits 0), actual: <verify><errorcode>: expected exit code 0, got 22. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 194

## Suggestion

In Curl.Protocol.Http.UnitLibrary's HttpDownloadConditions / HttpContentRange: treat a 416 whose 'Content-Range: bytes */<size>' equals the -C offset as 'the file is already complete'. Answer it before the -f check, with exit 0 and no body written, as curl 8.21.0 does.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1820.
