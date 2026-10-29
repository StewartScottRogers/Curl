---
id: BL-4172
title: Pass the activated extension transfer hooks from Curl.Console to the engine
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4094, BL-4171]
touches: [Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/ExtensionTransferHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed:
---
# BL-4172 — Pass the activated extension transfer hooks from Curl.Console to the engine

## Goal

`curl --<extension-option> URL` runs that extension's transfer hook on the transfer, and a command line with no extension option runs none.

## Context

BL-4094 built the hooks (ADR-1007): `ITransferHook`, `TransferHooks.ActivatedBy(ExtensionOptionValues, activators)` and `TransferHooks.Around` in `Curl.Extension.Abstractions/Transfer`, and the engine seam `extensionTransferHooks` (`Func<IProtocolHandler, IProtocolHandler>?`) on `EngineComposition.CreateProtocolHandlers`, where `null` runs no hook. BL-4171 makes each option group's `ExtensionOptionValues` reachable in Curl.Console. What is missing: a catalogue of the extensions' hook activators (one line per extension, a new `ExtensionTransferHookCatalogue.cs` in `Curl.Extension.Composition`, listed in `Tasks/SharedListFiles.txt`) and the Console passing `TransferHooks.ActivatedBy(values, catalogue).Around` for each option group down to `CreateProtocolHandlers` (find where `CurlComposition` reaches the engine's handler list). Curl.Api and Curl.Mcp get the same wiring in their own tasks.

## Acceptance criteria

- [ ] `Curl.Extension.Composition` has a transfer hook activator catalogue, one entry per line, listed in `Tasks/SharedListFiles.txt`.
- [ ] A Console unit test registers a test hook activated by a test extension option and shows a transfer with the option given runs it, and one without the option passes no hook to the engine.
- [ ] `dotnet build` is clean, the fast tests pass, and the changed libraries meet the quality gates by `Measure-CodeQuality.ps1`.

## Notes

## Log

- 2026-10-29: Created.
