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
completed: 2026-10-15
---
# BL-2323 — Make the engine's connection, DNS and timeout factories read EasyNetworkOptions instead of CommandLineOptions

## Goal

The engine's connection, DNS and timeout factories read `EasyNetworkOptions` instead of `CommandLineOptions`, and `Curl.Console` hands them the record `EasyOptionsFromCommandLine.NetworkOf` builds; behaviour is byte for byte unchanged.

## Context

ADR-0506 plan phase 1 step 4, split out of BL-2303 (which built `Curl.Console/EasyOptionsFromCommandLine.cs` and its network mapping, and settled the record's gaps in ADR-0513). The engine classes to retarget: `EngineComposition`'s `CreateTransports`, `CreateDnsResolver`, `CreateTcpConnector`, `CreateDohConnector`, `CreatePoolingConnector`, `LocalBindingOf`, `UnixSocketOf`, `AddressFamilyOf`, `ConnectTimeoutOf` and `DnsServerVariableResolverOf`; `TransferTimers.Of`; `OptionGroupConnectionSettings.Of`; `TransferContextFactory`'s connection fields. Factories that also read other groups (TLS, proxy, trace, HTTP version) take the network record beside `CommandLineOptions` until their own retarget (BL-2304 to BL-2309) moves the rest. `TransferTimers` also reads `-m`, which is `EasyTransferOptions.Timeout` (BL-2308's group): pass it as a `TimeSpan` until then.

Sizing measured in BL-2303: about 95 `CreateTransports` and 25 `CreateTcpConnector` call sites across `Curl.Console.UnitTests`, `Curl.Engine.UnitTests` and `Curl.Engine.IntegrationTests`. Console tests can pass `EasyOptionsFromCommandLine.NetworkOf(options)`; engine tests build the record directly.

Byte-for-byte points ADR-0513 settles: `ConnectTimeout` zero means "not set" (libcurl stores 0 and takes it as 300 s), so the traced `[CONNECTTIMEOUT]` timer still appears only when `--connect-timeout` was given; `HappyEyeballsTimeout` is 200 ms unless `--happy-eyeballs-timeout-ms` gave more than 0; `TcpKeepIdle`/`TcpKeepInterval` 60 s and `TcpKeepCount` 9 are libcurl's own defaults, which `TcpSocketOptions` must treat as the "not given" it treats 0 as today, or be shown equal; `IpTypeOfService`, `VlanPriority` and `MultipathTcp` are on the record.

## Acceptance criteria

- [x] The engine members named in Context take `EasyNetworkOptions`, not `CommandLineOptions`, for their network, DNS and timeout settings, and `Curl.Engine.UnitLibrary` references `Curl.Api.UnitLibrary`.
- [x] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.
- [x] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- Decisions in ADR-0526. `CreateTransports`, `CreateDnsResolver`, `CreateTcpConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` take the record beside `CommandLineOptions` (still read for TLS, proxy, trace, HTTP version, `-m`); `CreateUdpDatagramConnector`, `LocalBindingOf`, `UnixSocketOf`, `AddressFamilyOf`, `DnsServerVariableResolverOf` and the new `TcpSocketOptionsOf` take the record alone; `ConnectTimeoutOf` and `TransferTimers.Of` take it with `-m` as a `TimeSpan?`. `CreateDohConnector` reads no network option, so it stays as it was.
- `TransferContextFactory`'s connect timeout and `--connect-to` fields are split out to BL-2343: its per-URL `Create` has many callers, and threading the record through it is a change of its own.
- Test approach: call sites were rewritten mechanically to pass `EasyOptionsFromCommandLine.NetworkOf(<options>)`; `Curl.Engine.UnitTests` and `Curl.Engine.IntegrationTests` link `Curl.Console/EasyOptionsFromCommandLine.cs` for it. `AddressFamilyOf`, `LocalBindingOf` and `UnixSocketOf` tests build the record directly.
- Measured: curl 8.22.0 (Schannel) `--libcurl` writes no `CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS` for `--happy-eyeballs-timeout-ms 0` and `50L` for 50, so the record's 200 ms for 0 is curl's; the engine had used 0 ms. Keepalive: the record's 60 s / 9 probes equal `TcpSocketOptions`' defaults, so sockets are unchanged.
- Results: full fast suite 0 failures (Console 2547, Engine 652, Conformance 3129 passed); Measure-CodeQuality for both libraries: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. The engine's connection, DNS and timeout factories read EasyNetworkOptions; Curl.Console hands them EasyOptionsFromCommandLine.NetworkOf
