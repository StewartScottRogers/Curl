---
id: BL-4192
title: Pass the activated extension connection hook from Curl.Console to the engine
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4095, BL-4173]
touches: [Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/ExtensionOptionConflictException.cs, Curl.Console.UnitTests/ExtensionConnectionHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, .gitattributes, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4192 — Pass the activated extension connection hook from Curl.Console to the engine

## Goal

Giving an extension's option on the curl command line makes that extension's `IConnectionHook` carry the transfer, and two such options fail as a command-line error naming both.

## Context

BL-4095 added `ConnectionHooks` in `Curl.Extension.Abstractions/Connection` and the engine seam `extensionConnector` / `extensionDatagramConnector` on `EngineComposition.CreateProtocolHandlers` (ADR-1010). No host passes them yet. Mirror how BL-4173 wires the transfer hooks: a one-line-per-extension catalogue of connection-hook activators in `Curl.Extension.Composition`, `ConnectionHooks.ActivatedBy` over the option group's `ExtensionOptionValues`, and its `ConnectorOver` / `DatagramConnectorOver` passed in `CurlComposition`. The `ArgumentException` from two hooks becomes curl's option error (exit 2) with its message.

## Acceptance criteria

- [x] With a test extension's option given, a Console transfer runs over that extension's connector (unit test, no network).
- [x] With two connection-hook options given, curl exits 2 and the message names both options (unit test).
- [x] Without an extension option, the connector Console builds is unchanged (unit test).
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: mirror BL-4173. `ExtensionConnectionHookCatalogue` (one line per extension, `merge=union`, shared list file); `CurlComposition.ExtensionConnectionHooksOf` activates per option group and passes `ConnectorOver` / `DatagramConnectorOver` to `CreateProtocolHandlers` in all three dispatch paths, or `null` for both without a hook (`ExtensionConnectorOf`, `ExtensionDatagramConnectorOf`), so the connector is unchanged without an extension option.
- Two hooks: the `ArgumentException` becomes `ExtensionOptionConflictException` (message stripped of .NET's ` (Parameter 'values')` suffix); `CurlCommandRunner.TransferAllAsync` catches it where it builds the group's dispatch, writes `curl: <message>` and the try-help line even under `-s`, and ends the run with exit 2 (ADR-1014, decided by Claude under Stewart's delegation; no real curl to measure, as curl has no extension options).
- Touches widened to `Curl.Console/CurlCommandRunner.cs` (the runner must report the error), a new `Curl.Console/ExtensionOptionConflictException.cs`, `.gitattributes` and `Curl.Extension.Composition.UnitLibrary/CLAUDE.md` (as BL-4173 did); no task in Doing on origin/work/dark-factory names any of them.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Extension.Composition.UnitLibrary`: 0 failing members. Full build clean, fast tests all green on Windows; the tests use no paths or OS text, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5b7c3c09 ee312362 a8420816 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. An extension's option makes its IConnectionHook carry the Console transfer; two such options exit 2 naming both
