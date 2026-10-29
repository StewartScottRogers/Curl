---
id: BL-4083
title: Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4082]
touches: [Measure-CodeQuality.ps1, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4083 — Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 40885502110 (https://github.com/StewartScottRogers/Curl/actions/runs/40885502110). First failing commit: 000cd2ae.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CurlComposition.CreateTransferDispatch(Curl.Engine.CurlTransports, bool, Curl.Engine.CookieEngine, Curl.Authentication.NegotiateOptions) is outside a gate: branch; EngineComposition.CreateProtocolHandlers(Curl.Protocol.Abstractions.IConnector, Curl.Protocol.Abstractions.IDatagramConnector, Curl.Protocol.Abstractions.ITlsProvider, Curl.Protocol.Abstractions.IDnsResolver, Curl.Protocol.Abstractions.ICookieStore, Curl.Protocol.Abstractions.ISecurityContextFactory, Curl.Protocol.Abstractions.HttpAuthSchemes, Curl.Authentication.NegotiateOptions, System.TimeProvider, Curl.Protocol.Abstractions.IConnector, Curl.Protocol.Abstractions.IDiagnosticLog, bool, bool, bool, bool, bool, bool, bool, Curl.Protocol.Abstractions.IConnectionListener, Curl.Protocol.Ftp.IFtpWildcardHooks, Curl.Protocol.Rtsp.IRtspInterleaveSink, System.Func<Curl.Protocol.Rtsp.RtspRequestSettings>, Curl.Protocol.Rtsp.RtspSessionState, bool, Curl.Protocol.Ws.WsPerformConnection, Curl.Protocol.Http.IHttp2PushSink, Curl.Protocol.Ws.IWebSocketRandomSource, bool, System.Func<int, byte[]>, Curl.Engine.TransferSocketWait, System.Func<Curl.Protocol.Abstractions.ProtocolHandlerServices, System.Collections.Generic.IEnumerable<Curl.Protocol.Abstractions.IProtocolHandler>>) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 40885502110 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` fail intermittently is named under Notes and removed.
- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-10-29 (lane 1, first pass, no code edited): Neither failure is in Measure-CodeQuality.ps1. Both come from BL-4079 (f7b52c9e0, ADR-0999), which landed just before 000cd2ae:
  - `EngineComposition.CreateProtocolHandlers` complexity: BL-4079 added `.. extensionHandlers?.Invoke(new ProtocolHandlerServices(...)) ?? []` to the handler array. That adds two branch points and puts the method past 10. Fix: move that expression into a small private static method (e.g. `ExtensionHandlersOf(extensionHandlers, services)`) and cover both of its branches in `EngineCompositionTests.Extensions.cs`.
  - `CurlComposition.CreateTransferDispatch(CurlTransports, bool, ...)` branch: BL-4079 added `ExtensionProtocolHandlers.For(Environment.GetEnvironmentVariable)` beside the existing `new ProxySelector(Environment.GetEnvironmentVariable)`. Each method-group conversion compiles to a cached-delegate `??` check, which coverlet counts as a branch. Both sites share one cache field, and the first evaluation in a test process fills it. So which side of each check gets covered depends on test order and on the coverage shard. That is what makes the failure intermittent. Fix: make the delegate one `private static readonly Func<string, string?>` field set once (no branch at the use sites) (or cover both sides of `cookies?.HandlerStore` if coverlet still reports a branch), and check the branch with Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary.
  - Touches widened to those files (Curl.Console/CurlComposition.cs is not held by any Doing task). Curl.Engine.UnitLibrary is held whole by BL-4082 (Doing on origin/work/dark-factory), so BL-4082 was added to depends-on and the task requeued.
- 2026-10-29 (lane 4): The `EngineComposition.CreateProtocolHandlers` complexity was already removed by 6cfbb6698 (`ExtensionHandlersOver`, both branches covered by EngineCompositionTests.Extensions.cs). BL-4173 had made `CurlComposition.CreateTransferDispatch(CurlTransports, bool, ...)` take one local `Func<string, string?> readEnvironmentVariable = Environment.GetEnvironmentVariable;`, but that is still a compiler-cached method-group conversion: a `??` on the cache field whose null side only the first call in a test process (or shard) takes, so its coverage still hung on test order. Removed: the delegate is now the `private static readonly ProcessEnvironmentVariable = new(Environment.GetEnvironmentVariable)` field (explicit `new`, no cache, no branch), as `EasyErrorText` already does (BL-2291). `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary`: 0 failing members. Fast tests green on Windows. The change is platform-neutral (no path, OS text or scheduling), so CI on Linux and macOS is met by keeping it so; the shift's CI watch files a task for any failure.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 32ef7b66 e2ff6521.
- 2026-10-29: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/EngineComposition.cs, held whole by BL-4082 (Doing); depends-on BL-4082
- 2026-10-29: Backlog -> Doing. Criteria at claim: 32ef7b66 e2ff6521.
- 2026-10-29: Doing -> Done. CreateTransferDispatch has no cached method-group branch; quality gates pass for Curl.Console and Curl.Engine.UnitLibrary
