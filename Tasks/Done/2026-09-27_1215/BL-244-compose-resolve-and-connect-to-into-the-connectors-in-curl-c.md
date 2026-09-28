---
id: BL-244
title: Compose --resolve and --connect-to into the connectors in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-214, BL-202, BL-230]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-244 — Compose --resolve and --connect-to into the connectors in Curl.Console

## Goal

`--resolve` and `--connect-to` from the command line reach the resolver and connector BL-214 extended.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W15. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Not in the plan's W list: BL-214 implements the overrides in `Curl.Networking` but nothing composed them. Composition is in `Curl.Console/CurlComposition.cs`.

## Acceptance criteria

- [x] A Console test over `RecordingConnector` shows the overridden address and mapped host/port are used.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W15 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `CurlComposition.CreateTcpConnector(options, dnsResolver, tcpDialer, tlsProvider, timeProvider, proxyTunnelOptions)` builds the run's `TcpConnector` with `ResolveOverrides.Parse(options.ResolveEntries)` and `new ConnectToMappings(options.ConnectToEntries)`; `CreateTransports` uses it. No design decision needed, so no ADR: parsing and exit 49 behaviour were already measured and pinned in BL-214.
- Test fake choice: the overrides act inside `TcpConnector`, below the `IConnector` a `RecordingConnector` stands in for, so a `RecordingConnector` would only ever see the URL's host. `CurlCompositionConnectOverrideTests` instead runs the composed `TcpConnector` over `ScriptedTcpDialer`, whose `ScriptedConnector` records each address and port actually dialed - the same recording, one layer lower. Six tests: `--resolve` address, `--connect-to` port, both combined (resolve entry for the mapped host wins over the URL host's), neither, and exit 49 for a bad `--resolve` and a bad `--connect-to` port.
- Verified: `dotnet build -warnaserror` clean; fast tests green solution-wide (Curl.Console.UnitTests 809 passed); `dotnet format --verify-no-changes` clean for both projects; `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration` 100% line, 100% branch, 0 failing members, worst CRAP 10. Without `-IncludeIntegration` the only failing member is `DiskWriteOutFileOpener.TryOpen`, Integration-only by design (BL-280), unchanged here.
- Follow-up: BL-413 - `UdpDatagramConnector` (TFTP) takes no overrides, so `--resolve`/`--connect-to` do not reach TFTP yet.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --resolve and --connect-to now reach the TcpConnector the run composes
