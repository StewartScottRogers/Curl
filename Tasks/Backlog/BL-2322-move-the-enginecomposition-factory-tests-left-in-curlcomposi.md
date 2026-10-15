---
id: BL-2322
title: Move the EngineComposition factory tests left in CurlCompositionTests and CurlCompositionProxyTests to Curl.Engine.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2289]
touches: [Curl.Console.UnitTests, Curl.Engine.UnitTests, Curl.Console/CLAUDE.md, Curl.Engine.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-15
completed:
---
# BL-2322 — Move the EngineComposition factory tests left in CurlCompositionTests and CurlCompositionProxyTests to Curl.Engine.UnitTests

## Goal

Every test of an `EngineComposition` factory is in `Curl.Engine.UnitTests`; `Curl.Console.UnitTests` keeps only tests of `CurlComposition`'s own members (`CreateRunner`, `CreateTransferDispatch`, `CreateDispatcher`, `WarningLinesBeforeEachTransfer`, `SharingRunCookies`, `WriteOutTimeDialectFor`).

## Context

BL-2289 moved the factories to `Curl.Engine.UnitLibrary/EngineComposition.cs` and moved every test file that tested only them (now `Curl.Engine.UnitTests/EngineComposition*Tests.cs`). Left behind because they share one test class with runner tests: in `Curl.Console.UnitTests/CurlCompositionTests.cs` the `CreateProtocolHandlers_*`, `CreateTransports_*` and `CreateTlsProvider_*` tests, and the partials `CurlCompositionTests.ProxyPinnedPublicKey.cs` and `CurlCompositionTests.ProxyTlsHandshake.cs` (which call only `EngineComposition`); and any `EngineComposition`-only tests in `CurlCompositionProxyTests.cs` and `CurlCompositionProxyTests.Http2Tunnel.cs`. Move them into `EngineCompositionTests` (and `EngineCompositionProxyTests`) partials in `Curl.Engine.UnitTests`, namespace `Curl.Engine`, with the same test names and assertions; copy any Console-only fake they need into `Curl.Engine.UnitTests` as BL-2289 did for `LoopbackDnsResolver` and `ScriptedTcpDialer`. Also move the paragraphs of `Curl.Console/CLAUDE.md` that describe `EngineComposition` members' behaviour into `Curl.Engine.UnitLibrary/CLAUDE.md`.

## Acceptance criteria

- [ ] No test in `Curl.Console.UnitTests/CurlCompositionTests*.cs` or `CurlCompositionProxyTests*.cs` calls only `EngineComposition` members.
- [ ] The moved tests keep their names and assertions; the fast test counts of the two projects sum to the same total as before.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Curl.Console/CLAUDE.md` describes only `Curl.Console` code.

## Notes

## Log

- 2026-10-15: Created.
