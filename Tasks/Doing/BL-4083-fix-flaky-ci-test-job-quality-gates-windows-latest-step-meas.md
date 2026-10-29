---
id: BL-4083
title: Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-29
completed:
---
# BL-4083 — Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 40885502110 (https://github.com/StewartScottRogers/Curl/actions/runs/40885502110). First failing commit: 000cd2ae.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CurlComposition.CreateTransferDispatch(Curl.Engine.CurlTransports, bool, Curl.Engine.CookieEngine, Curl.Authentication.NegotiateOptions) is outside a gate: branch; EngineComposition.CreateProtocolHandlers(Curl.Protocol.Abstractions.IConnector, Curl.Protocol.Abstractions.IDatagramConnector, Curl.Protocol.Abstractions.ITlsProvider, Curl.Protocol.Abstractions.IDnsResolver, Curl.Protocol.Abstractions.ICookieStore, Curl.Protocol.Abstractions.ISecurityContextFactory, Curl.Protocol.Abstractions.HttpAuthSchemes, Curl.Authentication.NegotiateOptions, System.TimeProvider, Curl.Protocol.Abstractions.IConnector, Curl.Protocol.Abstractions.IDiagnosticLog, bool, bool, bool, bool, bool, bool, bool, Curl.Protocol.Abstractions.IConnectionListener, Curl.Protocol.Ftp.IFtpWildcardHooks, Curl.Protocol.Rtsp.IRtspInterleaveSink, System.Func<Curl.Protocol.Rtsp.RtspRequestSettings>, Curl.Protocol.Rtsp.RtspSessionState, bool, Curl.Protocol.Ws.WsPerformConnection, Curl.Protocol.Http.IHttp2PushSink, Curl.Protocol.Ws.IWebSocketRandomSource, bool, System.Func<int, byte[]>, Curl.Engine.TransferSocketWait, System.Func<Curl.Protocol.Abstractions.ProtocolHandlerServices, System.Collections.Generic.IEnumerable<Curl.Protocol.Abstractions.IProtocolHandler>>) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 40885502110 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` fail intermittently is named under Notes and removed.
- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 32ef7b66 e2ff6521.
