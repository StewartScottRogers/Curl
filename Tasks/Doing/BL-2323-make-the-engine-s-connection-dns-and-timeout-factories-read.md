---
id: BL-2323
title: Make the engine's connection, DNS and timeout factories read EasyNetworkOptions instead of CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2303]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Api.UnitLibrary/Options/Network, Curl.Api.UnitTests/Options/Network]
requirement: none
created: 2026-10-15
completed:
---
# BL-2323 — Make the engine's connection, DNS and timeout factories read EasyNetworkOptions instead of CommandLineOptions

## Goal

The engine's connection, DNS and timeout factories read `EasyNetworkOptions` instead of `CommandLineOptions`, and `Curl.Console` hands them the record `EasyOptionsFromCommandLine.NetworkOf` builds; behaviour is byte for byte unchanged.

## Context

ADR-0506 plan phase 1 step 4, split out of BL-2303 (which built `Curl.Console/EasyOptionsFromCommandLine.cs` and its network mapping, and settled the record's gaps in ADR-0513). The engine classes to retarget: `EngineComposition`'s `CreateTransports`, `CreateDnsResolver`, `CreateTcpConnector`, `CreateDohConnector`, `CreatePoolingConnector`, `LocalBindingOf`, `UnixSocketOf`, `AddressFamilyOf`, `ConnectTimeoutOf` and `DnsServerVariableResolverOf`; `TransferTimers.Of`; `OptionGroupConnectionSettings.Of`; `TransferContextFactory`'s connection fields. Factories that also read other groups (TLS, proxy, trace, HTTP version) take the network record beside `CommandLineOptions` until their own retarget (BL-2304 to BL-2309) moves the rest. `TransferTimers` also reads `-m`, which is `EasyTransferOptions.Timeout` (BL-2308's group): pass it as a `TimeSpan` until then.

Sizing measured in BL-2303: about 95 `CreateTransports` and 25 `CreateTcpConnector` call sites across `Curl.Console.UnitTests`, `Curl.Engine.UnitTests` and `Curl.Engine.IntegrationTests`. Console tests can pass `EasyOptionsFromCommandLine.NetworkOf(options)`; engine tests build the record directly.

Byte-for-byte points ADR-0513 settles: `ConnectTimeout` zero means "not set" (libcurl stores 0 and takes it as 300 s), so the traced `[CONNECTTIMEOUT]` timer still appears only when `--connect-timeout` was given; `HappyEyeballsTimeout` is 200 ms unless `--happy-eyeballs-timeout-ms` gave more than 0; `TcpKeepIdle`/`TcpKeepInterval` 60 s and `TcpKeepCount` 9 are libcurl's own defaults, which `TcpSocketOptions` must treat as the "not given" it treats 0 as today, or be shown equal; `IpTypeOfService`, `VlanPriority` and `MultipathTcp` are on the record.

## Acceptance criteria

- [ ] The engine members named in Context take `EasyNetworkOptions`, not `CommandLineOptions`, for their network, DNS and timeout settings, and `Curl.Engine.UnitLibrary` references `Curl.Api.UnitLibrary`.
- [ ] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.
- [ ] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
