---
id: BL-4195
title: Add a scheme hook so an option-activated extension serves a curl scheme (ws, wss) in place of curl's handler
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4173, BL-4192]
touches: [Curl.Extension.Abstractions.UnitLibrary/Scheme, Curl.Extension.Abstractions.UnitTests/Scheme, Curl.Extension.Composition.UnitLibrary/ExtensionSchemeHookCatalogue.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionSchemeHookTests.cs, Tasks/SharedListFiles.txt, .gitattributes]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4195 — Add a scheme hook so an option-activated extension serves a curl scheme (ws, wss) in place of curl's handler

## Goal

An extension whose option is given can serve a transfer for one of curl's own schemes (`ws://`, `wss://`) in place of curl's handler; without the option, curl's handler serves it unchanged.

## Context

Found by BL-4112's first pass: `Curl.Extension.Abstractions` has transfer hooks (headers, response body, result) and connection hooks (connector), but nothing lets an extension take a whole transfer for an existing scheme, and `ExtensionCatalogue` handlers are registered by scheme beside curl's. BL-4112 needs `ws`/`wss` run as RFC 8441/9220 extended CONNECT when its option is given. Follow the shape of `Connection/ConnectionHooks.cs` (ADR-1010): one active hook at most, refuse two with an `ArgumentException` naming the options. Waits on BL-4173 and BL-4192, which also change `Curl.Console/CurlComposition.cs`.

## Acceptance criteria

- [x] `Curl.Extension.Abstractions.UnitLibrary/Scheme` holds an interface by which an extension names the schemes it serves when its option is given and supplies the handler, and a catalogue that keeps the hook whose option was given; unit tests.
- [x] `Curl.Console` passes the activated scheme hook to the engine, which uses its handler for those schemes and curl's otherwise; with `CURL_EXTENSIONS=0` nothing changes; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the hook's shape.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Design (ADR-1048): `ISchemeHook` (option, schemes, `HandlerOver(curlHandler, TransferHookServices)`) and `SchemeHooks` (at most one, `Over(curlHandler)` routes the hook's schemes to its handler) in `Curl.Extension.Abstractions/Scheme`; `ExtensionSchemeHookCatalogue` empty until BL-4112 adds its line. The Console hands the hook to the engine through the existing `extensionTransferHooks` seam (`CurlComposition.ExtensionHandlerHooksOf`, scheme hook inside the transfer hooks), so `Curl.Engine` needed no change.
- Added `.gitattributes` to touches (no Doing task on origin/work/dark-factory names it): `Tasks/SharedListFiles.txt` requires its catalogue line to be marked `merge=union` there.
- `CURL_EXTENSIONS=0`: the parser then reads no extension option (`CurlCommandRunner`), so no activator sees its option; `RunAsync_NoExtensionOptionGiven_ServesWsWithCurlsHandler` and `ExtensionHandlerHooksOf_NoExtensionOptionGiven_PassesNoHooksToTheEngine` pin that nothing changes then.
- Measure-CodeQuality.ps1 on Curl.Extension.Abstractions.UnitLibrary, Curl.Extension.Composition.UnitLibrary and Curl.Console: 0 failing members. `dotnet build` clean; fast tests green (64 assemblies).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a262fa8a f6b43e01 7da2aab5 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. An option-activated extension can serve curl's own schemes (ws, wss) through ISchemeHook in place of curl's handler
