---
id: BL-4357
title: Engine: give the in-memory dialing runner's QUIC dialer the connection hook's datagram connector
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitTests/EngineCompositionTests.QuicThroughExtension.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4357 — Engine: give the in-memory dialing runner's QUIC dialer the connection hook's datagram connector

## Goal

Under an `EngineDialing` (the in-memory runner `CurlComposition.CreateRunner(..., ITcpDialer, IDnsResolver, IDatagramConnector, ...)` builds), QUIC opens its channels through an activated connection hook's datagram connector, as it already does on the real network (BL-4318).

## Context

`EngineComposition.DialersOf` (`Curl.Engine.UnitLibrary/EngineComposition.Transports.cs`) passes `extensionDatagramConnector` only to `NetworkDialersOf`; its `dialing` branch builds `CreateDatagramConnectorQuicDialer(traces, tlsClientOptions, dialing.DatagramConnector)` and ignores the hook. Use `QuicDatagramConnectorOf(dialing.DatagramConnector, extensionDatagramConnector) ?? dialing.DatagramConnector` there (curl's own `UdpDatagramConnector` stays `dialing.DatagramConnector`). Also check that the dialing runner in `Curl.Console/CurlComposition.cs` (the `CreateTransports` call near its `ProtocolDispatcher` with `extensionDatagramConnector: ExtensionDatagramConnectorOf(extensionConnectionHooks)`) hands the hook's connector to `CreateTransports`; if it does not, that is in scope too (add `Curl.Console/CurlComposition.cs` to `touches`).

BL-4355 (WireGuard UDP inside the tunnel) needs this for its unit test that runs `--wireguard ... --http3-only` through that runner with a fake WireGuard peer in front of the conformance HTTP/3 stand-in.

## Acceptance criteria

- [ ] With an `EngineDialing` and a connection hook whose datagram connector differs from curl's, the QUIC dialer opens its channel through the hook's connector, checked by a unit test in `EngineCompositionTests.QuicThroughExtension.cs`.
- [ ] Without a hook, or with a hook that keeps curl's connector, the QUIC dialer still opens through `dialing.DatagramConnector` (unit test).
- [ ] The four quality gates hold by Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4355's run of 2026-10-30, which found the dialing branch skips the hook.

## Log

- 2026-10-30: Created.
