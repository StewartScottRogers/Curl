---
id: BL-2995
title: Fail a --connect-to IPv6 literal with exit 4 when the feature mask hides IPv6, end to end (case 1454)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2982, BL-2856, BL-2996]
touches: [Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.Connectors.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs, Curl.Networking.UnitLibrary/UdpDatagramConnector.cs, Curl.Engine.UnitTests/EngineCompositionFeatureMaskTests.cs, Curl.Networking.UnitTests/TcpConnectorConnectToNotBuiltInTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2995 — Fail a --connect-to IPv6 literal with exit 4 when the feature mask hides IPv6, end to end (case 1454)

## Goal

With BL-2984's run-time feature mask hiding `IPv6`, `--connect-to localhost:80:[::1]:80 localhost` ends with exit 4 (`CURLE_NOT_BUILT_IN`) and `curl: (4) Use of IPv6 in *_CONNECT_TO without IPv6 support built-in`, so upstream case 1454 passes.

## Context

Split from BL-2982, which added the parse side in `Curl.Networking.UnitLibrary`: `new ConnectToMappings(mappings, hidesIpv6: true)` answers a matching destination starting with `[` with `ParseError` = `ConnectToMappings.Ipv6NotBuiltInMessage` and `ConnectDestination.ParseErrorCode` = `CurlExitCode.NotBuiltIn` (exit 49 otherwise). Left to do, held back from BL-2982 because other lanes held these files (BL-2919 EngineComposition.cs, BL-2986 TcpConnector.cs):

1. `TcpConnector.cs` (two sites, `ConnectAndNumberAsync` and the multiplexed path), `TcpConnector.UdpTunnel.cs` and `UdpDatagramConnector.cs` fail with `destination.ParseErrorCode` instead of the fixed `CurlExitCode.SetoptOptionSyntax` when the error is the destination's (a `--resolve` `ParseError` stays exit 49).
2. `EngineComposition.cs` builds its three `ConnectToMappings` with `hidesIpv6: dispatch.FeatureMask.HasFlag(EngineFeatureMask.Ipv6)` (ADR-0746; see how `TransferPerformer.Setup.cs` reads the mask for `Proxy`).
3. Run case 1454 through the case runner, check the output is platform-neutral, and hold it on the ratchet.

## Acceptance criteria

- [x] A unit test in `Curl.Networking.UnitTests` shows a `TcpConnector` given `ConnectToMappings(..., hidesIpv6: true)` and a `[::1]` destination fails the connect with `CurlExitCode.NotBuiltIn` and curl's message, and a malformed destination without the mask still fails with exit 49.
- [x] A unit test in `Curl.Engine.UnitTests` shows that with `EngineFeatureMask.Ipv6` the transfer ends with exit 4, and with no mask it connects as before.
- [x] Upstream case 1454 passes and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-21 (lane 7, sizing pass before any edit): requeued behind BL-2856. Criterion 3 needs the conformance harness to pass a case's `!IPv6` to `CurlComposition.CreateRunner(featureMask:)`; today nothing in `Curl.Conformance.*` sets `EngineFeatureMask` (no case runs masked; 375 is not on the ratchet either). That wiring (`UpstreamCurlPlatform.WithoutFeatures`, `PlatformFor`, `RunCurlAsync`) is BL-2856's planned work, and BL-2856 can finish with 1454's first difference in its Notes, so there is no cycle.
- Where the mask must reach: the connectors are built before the dispatch, so `dispatch.FeatureMask` is not available in `EngineComposition`. The harness path is `CurlComposition.CreateDialingTransferDispatch` (`Curl.Console/CurlComposition.cs`, which already has `featureMask`), which calls `EngineComposition.CreateTcpConnector` and `CreatePoolingConnector`. Plan: add an optional `EngineFeatureMask featureMask = EngineFeatureMask.None` to `CreateTcpConnector`, `CreatePoolingConnector` and `CreateUdpDatagramConnector`, each building `new ConnectToMappings(network.ConnectTo, hidesIpv6: featureMask.HasFlag(EngineFeatureMask.Ipv6))`, and pass it from `CreateDialingTransferDispatch`. Added `Curl.Console/CurlComposition.cs` to `touches` for that (no Doing task holds it).
- Connector change: at the four sites (`TcpConnector.cs` lines ~592 and ~666, `TcpConnector.UdpTunnel.cs` ~60, `UdpDatagramConnector.cs` ~118) fail with `_resolveOverrides.ParseError is null ? destination.ParseErrorCode : CurlExitCode.SetoptOptionSyntax`, so a `--resolve` error stays exit 49.

- 2026-10-21 (lane 3, third claim): BL-2996 had already done steps 1 to 3 (ResolveOverrides.ParseFailureCodeWith at the four connector sites, EngineComposition.ConnectToMappingsOf, the featureMask from CurlComposition, 1454 on the ratchet) and its `EngineCompositionConnectToFeatureMaskTests.cs` meets the Engine criterion. Added only what was missing: `Curl.Networking.UnitTests/TcpConnectorConnectToNotBuiltInTests.cs` (exit 4 with curl's message and nothing dialled under `hidesIpv6: true`; exit 49 for `b:x` without the mask). Test-only, so no production code changed and no Measure-CodeQuality run was needed (BL-2996's lane-2 run reported 0 failing members for the libraries it changed). Measured: `dotnet build Curl.slnx -warnaserror` clean, full fast test pass green (1454 included in Curl.Conformance.UnitTests). The test is platform-neutral: no paths, OS errors or sockets.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 28c72b3f dd7a4945 44346086 dd1cc079.
- 2026-10-21: Doing -> Backlog. Needs BL-2856 first: the conformance harness does not yet pass a case's !IPv6 as EngineFeatureMask, so case 1454 cannot pass
- 2026-10-21: now depends on BL-2996, which does this task's steps 1 to 3 for case 1454 (ConnectToMappingsOf, ResolveOverrides.ParseFailureCodeWith at the four connector sites, 1454 on the ratchet) and holds that work in the shift's stash. Running this task first would redo it and make the stash fail to apply. Once BL-2996 is Done, this task checks its criteria against that work and adds only what is missing (likely the TcpConnector unit test in Curl.Networking.UnitTests).
- 2026-10-21: Backlog -> Doing. Criteria at claim: 28c72b3f dd7a4945 44346086 dd1cc079.
- 2026-10-21: Doing -> Done. A --connect-to IPv6 literal under the feature mask's IPv6 bit fails with exit 4 end to end; case 1454 on the ratchet and a TcpConnector unit test pins it
