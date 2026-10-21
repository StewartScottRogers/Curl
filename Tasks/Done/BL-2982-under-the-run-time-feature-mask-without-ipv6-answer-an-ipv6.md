---
id: BL-2982
title: Under the run-time feature mask without IPv6, answer an IPv6 literal (--connect-to [::1]) with CURLE_NOT_BUILT_IN, exit 4 (case 1454)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2984]
touches: [Curl.Networking.UnitLibrary/ConnectDestination.cs, Curl.Networking.UnitLibrary/ConnectToMappings.cs, Curl.Networking.UnitTests/ConnectDestinationFeatureMaskTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2982 — Under the run-time feature mask without IPv6, answer an IPv6 literal (--connect-to [::1]) with CURLE_NOT_BUILT_IN, exit 4 (case 1454)

## Goal

With BL-2984's run-time feature mask hiding `IPv6`, `--connect-to localhost:80:[::1]:80 localhost` ends with exit 4 (`CURLE_NOT_BUILT_IN`), as upstream case 1454 expects of a curl built without IPv6.

## Context

Split from BL-2856. The `--connect-to` destination is resolved in `Curl.Networking.UnitLibrary` (`ConnectDestination.cs`; check where the IPv6 literal is read and add the file that does it to `touches` if it is another). curl 8.22.0 `lib/conncache.c` / `lib/url.c` `parse_connect_to_host_port`: without `USE_IPV6` a bracketed IPv6 host answers `CURLE_NOT_BUILT_IN`. Wire the mask from BL-2984 through to that code; without the mask nothing changes.

## Acceptance criteria

- [x] A unit test in `Curl.Networking.UnitTests` shows that with the mask hiding `IPv6` a `--connect-to` destination naming `[::1]` answers `CURLE_NOT_BUILT_IN`, and without the mask it connects as before.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- The IPv6 literal is read in `ConnectToMappings.cs`, not `ConnectDestination.cs`, so it was added to `touches` (no Doing task on `origin/work/dark-factory` names it).
- Design: `new ConnectToMappings(mappings, hidesIpv6: true)` answers a matching destination starting with `[` with `ParseError` = `Ipv6NotBuiltInMessage` ("Use of IPv6 in *_CONNECT_TO without IPv6 support built-in", curl 8.22.0 `parse_connect_to_host_port` without `USE_IPV6`) and the new `ConnectDestination.ParseErrorCode` = `CurlExitCode.NotBuiltIn`; the check runs before the bracket check, as upstream's `#else` does. `ParseErrorCode` defaults to exit 49, so every existing consumer is unchanged. Networking cannot see `EngineFeatureMask` (Engine references Networking), so the mask arrives as a bool.
- The consumers (`TcpConnector.cs`, held by BL-2986; `TcpConnector.UdpTunnel.cs`; `UdpDatagramConnector.cs`) and the engine wiring (`EngineComposition.cs`, held by BL-2919) are filed as BL-2994, with case 1454 on the ratchet.
- Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 100% line, 100% branch, 0 failing members (the first run had `ParseDestination` at complexity 14; the mask check moved to `ParseMatchedDestination`). Fast tests: 44 test assemblies passed.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: e8a15e41 dd1cc079.
- 2026-10-21: Doing -> Done. ConnectToMappings with IPv6 hidden answers a [::1] destination with exit 4 (NotBuiltIn); end-to-end wiring is BL-2994
