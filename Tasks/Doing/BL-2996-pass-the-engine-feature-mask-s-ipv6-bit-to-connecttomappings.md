---
id: BL-2996
title: Pass the engine feature mask's IPv6 bit to ConnectToMappings so upstream case 1454 exits 4, and list 959 to 961 once measured on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2996 — Pass the engine feature mask's IPv6 bit to ConnectToMappings so upstream case 1454 exits 4, and list 959 to 961 once measured on Linux and macOS

## Goal

Upstream case 1454 (`--connect-to localhost:80:[::1]:80` with `!IPv6`) exits 4 in the conformance harness, and 959, 960 and 961 (`!IDN`, `!win32`) are measured on Linux and macOS and listed if they pass.

## Context

BL-2856 made the harness take `!proxy`, `!IPv6` and `!IDN` off the platform and pass `EngineFeatureMask` to `CurlComposition.CreateRunner` (`UpstreamConformanceTests.MissingFeatureCases.cs`). 375 and 445 now pass. 1454 still exits 52 where 4 is expected: BL-2982 added `ConnectToMappings(mappings, hidesIpv6)`, but `Curl.Engine.UnitLibrary/EngineComposition.cs` builds every `ConnectToMappings` with the one-argument constructor (lines ~976, ~1049, ~1219), so the mask's `Ipv6` bit never reaches it. 959 to 961 are `SkippedOnThisPlatform` on Windows (`!win32`), so only CI on Linux and macOS can measure them.

## Acceptance criteria

- [ ] `EngineComposition` builds `ConnectToMappings` with `hidesIpv6` set from `EngineFeatureMask.Ipv6`, and an engine unit test pins exit 4 with the mask and no change without it.
- [ ] `$env:CURL_UPSTREAM_CASE='1454'` passes on Windows and 1454 is on `PassingUpstreamCases.txt`.
- [ ] 959, 960 and 961 are on `PassingUpstreamCases.txt` once a CI run shows them passing on Linux and macOS, or their first difference is in Notes.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: bc4e5432 2ede4afd 1c6ad2a8.
