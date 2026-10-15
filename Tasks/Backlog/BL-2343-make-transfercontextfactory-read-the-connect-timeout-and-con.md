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
completed:
---
# BL-2343 — Make TransferContextFactory read the connect timeout and --connect-to entries from EasyNetworkOptions

## Goal

`TransferContextFactory` takes each URL's `EasyNetworkOptions` and reads `TransferContext.ConnectTimeout` and the `--connect-to` entries `ConnectToTunnelsThroughProxy` maps from it instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2323 (ADR-0506 plan phase 1 step 4), which retargeted `EngineComposition`'s connection, DNS and timeout factories, `TransferTimers.Of` and `OptionGroupConnectionSettings.Of` to `EasyNetworkOptions` but left `TransferContextFactory` (`Curl.Engine.UnitLibrary/TransferContextFactory.cs`) because its `Create` is called per URL from `TransferPerformer` and many tests, so threading the record through it is its own change. The fields: `ConnectTimeout = options.ConnectTimeout` (the record's `ConnectTimeout` of zero means not set, ADR-0513, so map zero to `null`), and `options.ConnectToEntries` in `ConnectToTunnelsThroughProxy` (the record's `ConnectTo`). `MaxTime` is `-m`, BL-2308's group: leave it reading `CommandLineOptions`. Console code builds the record with `EasyOptionsFromCommandLine.NetworkOf(options)`; `Curl.Engine.UnitTests` links that file (BL-2323).

## Acceptance criteria

- [ ] `TransferContextFactory` reads the connect timeout and `--connect-to` entries from `EasyNetworkOptions`, not `CommandLineOptions`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

## Log

- 2026-10-15: Created.
