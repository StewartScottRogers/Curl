---
id: GF-0028
title: A host of only zero-width characters is sent instead of failing with exit 3
area: behaviour
key: behaviour:idn-host-mapping-to-empty
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-08_2029
regression: false
items: [behaviour:test763]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
task: BL-1821
tasks: [BL-1821]
---
# GF-0028 - A host of only zero-width characters is sent instead of failing with exit 3

## Summary

Curl differs from upstream curl in behaviour: A host of only zero-width characters is sent instead of failing with exit 3.

## Evidence

behaviour:test763 (URL is U+200B U+200C) expected 'upstream test763 passes', actual: <verify><errorcode>: expected exit code 3, got 52. Cause in code: Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.ToPunycode keeps the decoded name when IdnMapping.GetAscii throws, so a name that IDNA maps to nothing is accepted. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 763

## Suggestion

In Curl.Protocol.Abstractions.UnitLibrary's CurlUrlHost, reject a host whose IDNA mapping (UTS #46, ignored code points removed) leaves an empty name: a malformed URL, exit 3. Keep the existing acceptance of a%80b and a%FFb.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 0 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1821.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
