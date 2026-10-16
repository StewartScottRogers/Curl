---
id: BL-2343
title: Make TransferContextFactory read the connect timeout and --connect-to entries from EasyNetworkOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2323]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2343 — Make TransferContextFactory read the connect timeout and --connect-to entries from EasyNetworkOptions

## Goal

`TransferContextFactory` takes each URL's `EasyNetworkOptions` and reads `TransferContext.ConnectTimeout` and the `--connect-to` entries `ConnectToTunnelsThroughProxy` maps from it instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2323 (ADR-0506 plan phase 1 step 4), which retargeted `EngineComposition`'s connection, DNS and timeout factories, `TransferTimers.Of` and `OptionGroupConnectionSettings.Of` to `EasyNetworkOptions` but left `TransferContextFactory` (`Curl.Engine.UnitLibrary/TransferContextFactory.cs`) because its `Create` is called per URL from `TransferPerformer` and many tests, so threading the record through it is its own change. The fields: `ConnectTimeout = options.ConnectTimeout` (the record's `ConnectTimeout` of zero means not set, ADR-0513, so map zero to `null`), and `options.ConnectToEntries` in `ConnectToTunnelsThroughProxy` (the record's `ConnectTo`). `MaxTime` is `-m`, BL-2308's group: leave it reading `CommandLineOptions`. Console code builds the record with `EasyOptionsFromCommandLine.NetworkOf(options)`; `Curl.Engine.UnitTests` links that file (BL-2323).

## Acceptance criteria

- [x] `TransferContextFactory` reads the connect timeout and `--connect-to` entries from `EasyNetworkOptions`, not `CommandLineOptions`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

- `TransferContextFactory.Create` takes `EasyNetworkOptions network` after `proxyOptions`; `CurlCommandRunner`'s two call sites pass `EasyOptionsFromCommandLine.NetworkOf(options)`. `ConnectToTunnelsThroughProxy` now takes the record and reads `ConnectTo`.
- Connect timeout: the record's zero (not set, ADR-0513) maps to a `null` `TransferContext.ConnectTimeout` (`ConnectTimeoutOf`). `NetworkOf` drops a non-positive `--connect-timeout`, so `--connect-timeout 0` now gives `null` where it gave zero; every reader (HTTP, FTP, TFTP deadlines) already treats a non-positive value as not given, so behaviour is unchanged. No ADR: it follows ADR-0513.
- Tests: every `Create` call passes a network record; the `ConnectToTunnelsThroughProxy` tests build the record directly; new `Create_NetworkOptionsConnectTimeout_IsTheContextsConnectTimeoutWithZeroAsNull`. Engine tests 718 passed, Console 2826, Conformance 3129. Quality: 0 failing members across both libraries.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TransferContextFactory reads the connect timeout and --connect-to entries from EasyNetworkOptions
