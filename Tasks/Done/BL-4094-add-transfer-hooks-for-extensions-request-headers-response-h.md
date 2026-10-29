---
id: BL-4094
title: Add transfer hooks for extensions: request headers, response headers, body and timings
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.Abstractions.UnitLibrary/Curl.Extension.Abstractions.UnitLibrary.csproj, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md, Curl.Extension.Abstractions.UnitTests/Curl.Extension.Abstractions.UnitTests.csproj, Curl.Extension.Abstractions.UnitTests/Transfer, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4094 — Add transfer hooks for extensions: request headers, response headers, body and timings

## Goal

An extension activated by its option can add request headers, read response headers, observe or transform the response body, and read the transfer's timings, without curl's behaviour changing when no extension option is given.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Needed by SSE, the HTTP cache, digests, checksums, HAR, OpenTelemetry, record and replay, OAuth and most other option extensions. Find the narrowest existing seam in the engine (`Curl.Engine.UnitLibrary`) and expose it through `ProtocolHandlerServices` or a sibling record, as ADR-0999 did for handlers.

## Acceptance criteria

- [x] A test extension adds a request header, sees the status and headers, rewrites the body and reads the timings of one fake transfer (engine unit test, no network).
- [x] With no extension option given, a transfer runs no extension hook (unit test proves the hook list is empty).
- [x] Hooks run after curl's own processing on the way in and before it on the way out, recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan (ADR-1007): the engine may not reference an extension (`VerifyExtensionIsolation`), so its seam is the narrowest one, a `Func<IProtocolHandler, IProtocolHandler>? extensionTransferHooks` on `EngineComposition.CreateProtocolHandlers` that every handler (curl's and the extensions') goes through, inside the end-point reporting; `null` runs no hook. The contract is `Curl.Extension.Abstractions/Transfer`: `ITransferHook` (`RequestHeadersFor`, `ResponseBodyOver`, `TransferFinished`, each a no-op by default), `TransferHooks` (`ActivatedBy` option values, `Around` a handler, giving it back unchanged with no hook) and the internal `TransferHookingProtocolHandler` and `HookedTransferContext`.
- Touches widened to the library's csproj (it now references `Curl.Protocol.Abstractions`, for `IProtocolHandler`, `ITransferContext` and `TransferResult`), its CLAUDE.md (which said it references nothing of Curl's) and the test csproj (global usings). No other task in Doing on origin/work/dark-factory named them (BL-4093 holds only the `Description` subfolders).
- Criterion 1: the fake transfer test (header added, status and headers seen, body rewritten, timings read) is `TransferHooksTests.ExecuteAsync_WithAHook_AddsItsHeaderRewritesTheBodyAndReadsStatusHeadersAndTimings` in Curl.Extension.Abstractions.UnitTests, because the engine's tests may not reference an extension project; the engine's side (every handler through the seam) is `EngineCompositionTests.CreateProtocolHandlers_WithExtensionTransferHooks_PutsEveryHandlerThroughThem`.
- Criterion 2: `ActivatedBy_WithNoExtensionOptionGiven_HoldsNoHookAndGivesTheHandlerBack` and `CreateProtocolHandlers_WithoutExtensionTransferHooks_RunsEveryHandlerWithoutAHook`.
- Quality: `CreateProtocolHandlers` measured complexity 12 (the report folds lambdas in), so the two null checks moved to `ThroughTransferHooks` and `ExtensionHandlersOver`; Measure-CodeQuality.ps1 then gave Curl.Engine.UnitLibrary and Curl.Extension.Abstractions.UnitLibrary 100/100, 0 failing members.
- Fast tests: 50 test projects passed, 0 failed. Change is platform-neutral (no path, OS error or certificate).
- Hosts' wiring (building `TransferHooks` from each option group in Curl.Console, Curl.Api, Curl.Mcp) is not in this task's touches and is filed as BL-4173 (Console; it builds on BL-4171).


## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a58a140f b8a5dc4a 0380c217 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Extensions can add request headers, read status, headers and timings, and rewrite the body through ITransferHook; the engine runs no hook without one
