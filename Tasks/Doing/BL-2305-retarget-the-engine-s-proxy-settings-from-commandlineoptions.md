---
id: BL-2305
title: Retarget the engine's proxy settings from CommandLineOptions to EasyProxyOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2304, BL-2298]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Proxy, Curl.Api.UnitTests/Options/Proxy]
requirement: none
created: 2026-10-15
completed:
---
# BL-2305 — Retarget the engine's proxy settings from CommandLineOptions to EasyProxyOptions

## Goal

The engine reads its proxy settings from `EasyProxyOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds that record; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2304 and uses the rules for every retarget task written in BL-2303's Context. Engine classes reading proxy settings (as of 2026-10-15): `TransferProxySelection`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`, `Socks5AuthenticationMapping`, the proxy selection in `TransferPerformer`, `TransferContextFactory.ConnectToTunnelsThroughProxy`. Proxy environment variables (`http_proxy`, `NO_PROXY`, ...) are read by libcurl, so their reading stays in the engine; the record carries only what `CURLOPT_PROXY`/`NOPROXY` set. Command-line options: `-x`, `--proxy-*` non-TLS options, `--preproxy`, `--noproxy`, `-p`, `--suppress-connect-headers`, `--proxy-header`, `--socks4*`, `--socks5*`, `--haproxy-protocol`, `--haproxy-clientip`.

## Acceptance criteria

- [ ] The engine classes named in Context take `EasyProxyOptions`, not `CommandLineOptions`, for their proxy settings.
- [ ] `EasyOptionsFromCommandLine` maps every proxy command-line option named in Context, each with a test.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
