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
completed: 2026-10-15
---
# BL-2305 — Retarget the engine's proxy settings from CommandLineOptions to EasyProxyOptions

## Goal

The engine reads its proxy settings from `EasyProxyOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds that record; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2304 and uses the rules for every retarget task written in BL-2303's Context. Engine classes reading proxy settings (as of 2026-10-15): `TransferProxySelection`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`, `Socks5AuthenticationMapping`, the proxy selection in `TransferPerformer`, `TransferContextFactory.ConnectToTunnelsThroughProxy`. Proxy environment variables (`http_proxy`, `NO_PROXY`, ...) are read by libcurl, so their reading stays in the engine; the record carries only what `CURLOPT_PROXY`/`NOPROXY` set. Command-line options: `-x`, `--proxy-*` non-TLS options, `--preproxy`, `--noproxy`, `-p`, `--suppress-connect-headers`, `--proxy-header`, `--socks4*`, `--socks5*`, `--haproxy-protocol`, `--haproxy-clientip`.

## Acceptance criteria

- [x] ~~The engine classes named in Context take `EasyProxyOptions`, not `CommandLineOptions`, for their proxy settings.~~ - split out to BL-2325 (see Notes).
- [x] `EasyOptionsFromCommandLine` maps every proxy command-line option named in Context, each with a test.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- 2026-10-15, split (rule 4, before any code): `Curl.Engine.UnitLibrary` has no reference to `Curl.Api.UnitLibrary` until BL-2323 (still in Backlog), so the engine classes cannot take `EasyProxyOptions` in this run, as BL-2303 and BL-2304 found for their groups. This task delivers the mapper and ADR-0515; BL-2325 (depends on this and BL-2323) retargets the engine's proxy classes.
- Decided (ADR-0515, by Claude under Stewart's delegation): `ProxyType` is the last proxy option's kind (a scheme in the address outranks it in libcurl, not here); `--proxy-http2` makes an `-x` proxy `Https2` and leaves `--proxy1.0`/`--socks*` alone (curl 8.22.0 would also turn an earlier `--socks5` into HTTPS2; `CommandLineOptions` does not keep the order, so the engine's behaviour wins); no `--socks5-basic`/`--socks5-gssapi` keeps libcurl's `Basic | GssApi`; `HeaderOption` stays `Separate` and `ProxyPort` 0, as curl's tool sets nothing else. Environment variables stay the engine's; credentials and `--proxy-service-name` are the authentication group's.
- `EasyProxyOptions` needed no new member. Agrees with `LibcurlSourceCode`'s `CURLOPT_PROXY`, `PROXYTYPE`, `HTTPPROXYTUNNEL`, `PRE_PROXY`, `NOPROXY`, `SUPPRESS_CONNECT_HEADERS`, `HAPROXY*`, `SOCKS5_GSSAPI_NEC`, `SOCKS5_AUTH` and `PROXYHEADER` lines; `--libcurl` writes no `PROXYTYPE` for `--proxy-http2` (the Schannel reference refuses the option).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console` - 100% line and branch, 768 members, 0 failing, worst CRAP 10. Fast tests: 39 assemblies green (Console 2468 passed, 25 skipped). Ratchet file unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyOptionsFromCommandLine.ProxyOf maps every proxy-group option onto EasyProxyOptions (ADR-0515); engine retarget split to BL-2325
