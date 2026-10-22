---
id: BL-2356
title: Make EngineComposition's factory methods and OptionGroupConnectionSettings stop taking CommandLineOptions
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Api.UnitLibrary, Curl.Api.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2356 — Make EngineComposition's factory methods and OptionGroupConnectionSettings stop taking CommandLineOptions

## Goal

`EngineComposition`'s non-trace methods (CreateTransports both overloads, CreateDnsResolver, CreateDohConnector, CreateProxyTunnelOptions, CreateDatagramConnectorQuicDialer, WaitsForMultiplexing, the two methods taking `CommandLineOptions options` near lines 853 and 914) and `OptionGroupConnectionSettings.Of` no longer take `CommandLineOptions`, and the engine no longer uses `Curl.Cli`'s `InterfaceBinding`.

## Context

ADR-0506; one of the four pieces BL-2310 needs before the engine can drop its `Curl.Cli` reference. Measured 2026-10-15 by BL-2310. Members still read from `CommandLineOptions` here include UseAlpn, Insecure, SslSessionsFile, Passphrase, Engine, ProxyTunnel, ProxyCredentials, ProxyType, Proxy, NoProxy, UnixSocketPath, ConnectToEntries, Interface, Parallel and ParallelImmediate. Read each from the `Curl.Api` easy groups already passed in (EasyNetworkOptions, EasyTlsOptions, EasyHttpOptions, EasyProxyOptions, EasyAuthenticationOptions), adding a field to a group where libcurl has the matching `CURLOPT_*`; for a curl-tool-only setting (parallel, `--ssl-sessions`) define an engine-owned settings type in `Curl.Engine.UnitLibrary` that `Curl.Console` fills. Parse `--interface` with an engine-owned or `Curl.Api` type rather than `Curl.Cli.InterfaceBinding`. Leave the `Traces*` methods to BL-2355.

## Acceptance criteria

- [x] None of the methods named in Goal, and no part of `OptionGroupConnectionSettings.cs`, names `CommandLineOptions` or `InterfaceBinding`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1` passes all four gates for every library changed, measured in one call.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- Decision (ADR-0538): ALPN and `-A` come from `EasyHttpOptions`; `-Z`, `--parallel-immediate`, `--ssl-sessions` and the `-m` trace limit are an engine-owned `EngineToolOptions`; `--interface` is parsed by `EngineInterfaceBinding`. `TransferPerformer.RefuseMalformedInterface` now reparses `options.Interface.Value` with it; `CommandLineOptions` itself is BL-2310's.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Transport factories read EngineToolOptions and easy groups; build clean, fast tests green, quality gates pass
