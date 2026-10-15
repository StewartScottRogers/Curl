---
id: BL-2325
title: Make the engine's proxy classes read EasyProxyOptions instead of CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2305, BL-2323]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Api.UnitLibrary/Options/Proxy, Curl.Api.UnitTests/Options/Proxy]
requirement: none
created: 2026-10-15
completed:
---
# BL-2325 — Make the engine's proxy classes read EasyProxyOptions instead of CommandLineOptions

## Goal

The engine's proxy classes read `EasyProxyOptions` instead of `CommandLineOptions`, and `Curl.Console` hands them the record `EasyOptionsFromCommandLine.ProxyOf` builds; behaviour is byte for byte unchanged.

## Context

ADR-0506 plan phase 1 step 4, split out of BL-2305 (which built `EasyOptionsFromCommandLine.ProxyOf` and settled the mapping in ADR-0515). Engine classes to retarget: `TransferProxySelection`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`, `Socks5AuthenticationMapping`, the proxy selection in `TransferPerformer`, and `TransferContextFactory.ConnectToTunnelsThroughProxy`. Follow the retarget rules in BL-2303's Context. Depends on BL-2323, which gives `Curl.Engine.UnitLibrary` its reference to `Curl.Api.UnitLibrary`.

Points ADR-0515 settles that this retarget must honour:
- The proxy environment variables stay the engine's to read (libcurl reads them); the record carries only what `CURLOPT_PROXY`/`CURLOPT_NOPROXY` set, `null` meaning "the environment decides".
- `ProxyType` is the option's kind (`--proxy1.0` is `Http10`, `--socks*` the SOCKS kinds) and a scheme in `Proxy` outranks it; `--proxy-http2` with `-x` is `Https2`, which the engine reads where it reads `ProxyHttp2` today.
- `Socks5Authentication` with neither `--socks5-basic` nor `--socks5-gssapi` is libcurl's `Basic | GssApi`; check that `Socks5AuthenticationMapping` reads that default as it reads "neither given" today.
- Proxy credentials and `--proxy-service-name` are `EasyAuthenticationOptions`', proxy TLS `EasyTlsOptions.Proxy`: keep passing them beside the record until their own retargets (BL-2324 and the authentication group's).

## Acceptance criteria

- [ ] `TransferProxySelection`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`, `Socks5AuthenticationMapping`, `TransferPerformer`'s proxy selection and `TransferContextFactory.ConnectToTunnelsThroughProxy` take `EasyProxyOptions`, not `CommandLineOptions`, for their proxy settings.
- [ ] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.
- [ ] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
