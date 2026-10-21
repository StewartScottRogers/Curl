---
id: BL-2994
title: Fail a --connect-to IPv6 literal with exit 4 when the feature mask hides IPv6, end to end (case 1454)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2982]
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs, Curl.Networking.UnitLibrary/UdpDatagramConnector.cs, Curl.Engine.UnitTests/EngineCompositionFeatureMaskTests.cs, Curl.Networking.UnitTests/TcpConnectorConnectToNotBuiltInTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2994 — Fail a --connect-to IPv6 literal with exit 4 when the feature mask hides IPv6, end to end (case 1454)

## Goal

With BL-2984's run-time feature mask hiding `IPv6`, `--connect-to localhost:80:[::1]:80 localhost` ends with exit 4 (`CURLE_NOT_BUILT_IN`) and `curl: (4) Use of IPv6 in *_CONNECT_TO without IPv6 support built-in`, so upstream case 1454 passes.

## Context

Split from BL-2982, which added the parse side in `Curl.Networking.UnitLibrary`: `new ConnectToMappings(mappings, hidesIpv6: true)` answers a matching destination starting with `[` with `ParseError` = `ConnectToMappings.Ipv6NotBuiltInMessage` and `ConnectDestination.ParseErrorCode` = `CurlExitCode.NotBuiltIn` (exit 49 otherwise). Left to do, held back from BL-2982 because other lanes held these files (BL-2919 EngineComposition.cs, BL-2986 TcpConnector.cs):

1. `TcpConnector.cs` (two sites, `ConnectAndNumberAsync` and the multiplexed path), `TcpConnector.UdpTunnel.cs` and `UdpDatagramConnector.cs` fail with `destination.ParseErrorCode` instead of the fixed `CurlExitCode.SetoptOptionSyntax` when the error is the destination's (a `--resolve` `ParseError` stays exit 49).
2. `EngineComposition.cs` builds its three `ConnectToMappings` with `hidesIpv6: dispatch.FeatureMask.HasFlag(EngineFeatureMask.Ipv6)` (ADR-0746; see how `TransferPerformer.Setup.cs` reads the mask for `Proxy`).
3. Run case 1454 through the case runner, check the output is platform-neutral, and hold it on the ratchet.

## Acceptance criteria

- [ ] A unit test in `Curl.Networking.UnitTests` shows a `TcpConnector` given `ConnectToMappings(..., hidesIpv6: true)` and a `[::1]` destination fails the connect with `CurlExitCode.NotBuiltIn` and curl's message, and a malformed destination without the mask still fails with exit 49.
- [ ] A unit test in `Curl.Engine.UnitTests` shows that with `EngineFeatureMask.Ipv6` the transfer ends with exit 4, and with no mask it connects as before.
- [ ] Upstream case 1454 passes and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
