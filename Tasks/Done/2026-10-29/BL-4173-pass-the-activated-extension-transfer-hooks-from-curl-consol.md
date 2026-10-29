---
id: BL-4173
title: Pass the activated extension transfer hooks from Curl.Console to the engine
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4094, BL-4171]
touches: [Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/ExtensionTransferHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Tasks/SharedListFiles.txt, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, .gitattributes]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4173 — Pass the activated extension transfer hooks from Curl.Console to the engine

## Goal

`curl --<extension-option> URL` runs that extension's transfer hook on the transfer, and a command line with no extension option runs none.

## Context

BL-4094 built the hooks (ADR-1007): `ITransferHook`, `TransferHooks.ActivatedBy(ExtensionOptionValues, activators)` and `TransferHooks.Around` in `Curl.Extension.Abstractions/Transfer`, and the engine seam `extensionTransferHooks` (`Func<IProtocolHandler, IProtocolHandler>?`) on `EngineComposition.CreateProtocolHandlers`, where `null` runs no hook. BL-4171 makes each option group's `ExtensionOptionValues` reachable in Curl.Console. What is missing: a catalogue of the extensions' hook activators (one line per extension, a new `ExtensionTransferHookCatalogue.cs` in `Curl.Extension.Composition`, listed in `Tasks/SharedListFiles.txt`) and the Console passing `TransferHooks.ActivatedBy(values, catalogue).Around` for each option group down to `CreateProtocolHandlers` (find where `CurlComposition` reaches the engine's handler list). Curl.Api and Curl.Mcp get the same wiring in their own tasks.

## Acceptance criteria

- [x] `Curl.Extension.Composition` has a transfer hook activator catalogue, one entry per line, listed in `Tasks/SharedListFiles.txt`.
- [x] A Console unit test registers a test hook activated by a test extension option and shows a transfer with the option given runs it, and one without the option passes no hook to the engine.
- [x] `dotnet build` is clean, the fast tests pass, and the changed libraries meet the quality gates by `Measure-CodeQuality.ps1`.

## Notes

- Added `.gitattributes` (the catalogue's `merge=union` line, which every shared list file needs, ADR-0689) and `Curl.Extension.Composition.UnitLibrary/CLAUDE.md` (documents the new catalogue) to touches; no task in Doing on origin/work/dark-factory names either.
- Design (defaults taken): `CurlComposition.ExtensionTransferHooksOf(options)` builds `TransferHooks.ActivatedBy(ExtensionOptionValuesOf(options), ExtensionTransferHookCatalogue.Entries)` and hands the engine `Around`, or `null` when the group activated no hook, so a command line with no extension option passes no hook at all (the engine's `null` = no hook). Wired into both production compositions (transports and dialing) and the fake-connector `CreateRunner`, which gains `extensionOptions` and `extensionTransferHookActivators` so tests can register their own. The catalogue is public (unlike `ExtensionCatalogue`) because the Console reads `Entries` directly. No ADR: ADR-1007 already decides the seam.
- Quality: the first `Measure-CodeQuality.ps1` run failed `CreateTransferDispatch(CurlTransports, bool, ...)` at 5/6 branches: `Environment.GetEnvironmentVariable` was converted to a delegate twice in one expression, and the compiler's shared method-group cache made the second check's null side unreachable. Converting it once into a local fixed it; second run: Curl.Console 100% line, 100% branch, 0 failing; Curl.Extension.Composition 100/100, 0 failing.
- The new tests use no file system, path, OS error or certificate (ScriptedConnector over HTTP), so they are platform-neutral; fast tests green on Windows.
## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: fbe6bcf5 e4c824c2 c13bc3be.
- 2026-10-29: Doing -> Done. Console hands the engine the hooks each group's extension options activate (ExtensionTransferHookCatalogue), none without one; build clean, fast tests green, gates met.
