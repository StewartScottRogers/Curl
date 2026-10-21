---
id: BL-2996
title: Pass the engine feature mask's IPv6 bit to ConnectToMappings so upstream case 1454 exits 4, and list 959 to 961 once measured on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2997]
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Console/CurlComposition.cs, Curl.Networking.UnitLibrary/ResolveOverrides.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs, Curl.Networking.UnitLibrary/UdpDatagramConnector.cs, Curl.Networking.UnitTests/ResolveOverridesParseFailureCodeTests.cs]
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

- [x] `EngineComposition` builds `ConnectToMappings` with `hidesIpv6` set from `EngineFeatureMask.Ipv6`, and an engine unit test pins exit 4 with the mask and no change without it.
- [x] `$env:CURL_UPSTREAM_CASE='1454'` passes on Windows and 1454 is on `PassingUpstreamCases.txt`.
- [ ] 959, 960 and 961 are on `PassingUpstreamCases.txt` once a CI run shows them passing on Linux and macOS, or their first difference is in Notes.

## Notes

- 2026-10-21 (lane 6, first claim): `touches` widened to `Curl.Console/CurlComposition.cs` and four `Curl.Networking.UnitLibrary` files plus one new `Curl.Networking.UnitTests` file; no task in Doing on `origin/work/dark-factory` named any of them. Why: the harness dials through `CurlComposition.CreateDialingTransferDispatch`, which calls `EngineComposition.CreateTcpConnector`/`CreatePoolingConnector`, so the mask has to be passed from there; and once it reached `ConnectToMappings`, case 1454 went from exit 52 to exit 49 with the right message, because `TcpConnector` (two sites), `TcpConnector.UdpTunnel` and `UdpDatagramConnector` answered every parse error with `SetoptOptionSyntax`, ignoring `ConnectDestination.ParseErrorCode`.
- Work done, left uncommitted for the shift's stash (rule 6): `EngineComposition.ConnectToMappingsOf(network, featureMask)` (new; `hidesIpv6` from `EngineFeatureMask.Ipv6`), used by `CreatePoolingConnector` and `CreateTcpConnector`, which take an optional `featureMask` (default `None`); `CurlComposition.CreateDialingTransferDispatch` passes its mask to both; `ResolveOverrides.ParseFailureCodeWith(destination)` (new: 49 for a bad `--resolve` entry, else the destination's code) used at the four connector sites. Tests: `Curl.Engine.UnitTests/EngineCompositionConnectToFeatureMaskTests.cs` (exit 4 with the mask, dials `[::1]:80` without it, and `ConnectToMappingsOf` under `Ipv6|Proxy` and `Proxy` alone) and `Curl.Networking.UnitTests/ResolveOverridesParseFailureCodeTests.cs` (both branches). 1454 added to `PassingUpstreamCases.txt`. `CreateUdpDatagramConnector` and `TransferContextFactory`/`AltSvcTransferCache` keep the one-argument mappings: they never see the mask and 1454 does not reach them.
- Measured in this run: `dotnet build` clean; `CURL_UPSTREAM_CASE=1454` passes (`Curl.Conformance.UnitTests`, 3141 passed); `EngineCompositionConnect*` 13 passed; `Curl.Networking.UnitTests` `ResolveOverrides|ConnectTo` 127 passed. Not yet run: the full fast test pass and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Networking.UnitLibrary,Curl.Console` — the next claim runs both before committing.
- Criterion 3 cannot be met in a lane: 959 to 961 are `!win32`, and no finished `CI` run on `work/dark-factory` included BL-2856 (e75f7d1bd) yet (runs 39691487971 in progress, earlier ones cancelled). Listing them unmeasured risks red CI (AF-0226), so the measurement is BL-2997, now in `depends-on`; once it is Done this criterion is its result.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: bc4e5432 2ede4afd 1c6ad2a8.
- 2026-10-21: Doing -> Backlog. Waits on BL-2997: cases 959-961 are !win32 and need a Linux/macOS CI measurement; the 1454 fix is done and left for the stash
