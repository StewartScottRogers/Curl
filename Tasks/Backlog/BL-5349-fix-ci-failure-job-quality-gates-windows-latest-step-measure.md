---
id: BL-5349
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-17
completed:
---
# BL-5349 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 44045672058 (https://github.com/StewartScottRogers/Curl/actions/runs/44045672058). First failing commit: 3f0cbe7b.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; TransferContextFactory.Create(Curl.Engine.EngineTransferSettings, Curl.Api.Options.Http.EasyHttpOptions, Curl.Api.Options.Behaviour.EasyBehaviourOptions, Curl.Api.Options.Behaviour.EasyTransferOptions, Curl.Api.Options.Protocols.EasyProtocolOptions, Curl.Api.Options.Proxy.EasyProxyOptions, Curl.Api.Options.Authentication.EasyAuthenticationOptions, Curl.Api.Options.Network.EasyNetworkOptions, Curl.Protocol.Abstractions.CurlUrl, System.IO.Stream, Curl.Protocol.Abstractions.ByteRange, System.Nullable<long>, System.IO.Stream, Curl.Protocol.Abstractions.HttpRequestBody, System.IO.Stream, Curl.Protocol.Abstractions.ProxyEndpoint, System.Func<System.IO.Stream, System.IO.Stream>, Curl.Protocol.Abstractions.ITransferProgress, Curl.Protocol.Abstractions.ITransferEvents, Curl.Core.LowSpeedWatchdog, System.Threading.CancellationToken, Curl.Core.MaxTimeWatchdog, System.Net.NetworkCredential, System.Collections.Generic.IReadOnlyList<string>, Curl.Engine.AltSvcTransferCache, Curl.Output.StyledHeaderLines, Curl.Protocol.Abstractions.SshOptions, Curl.Engine.EngineFeatureMask) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 44045672058 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-17: Created.
